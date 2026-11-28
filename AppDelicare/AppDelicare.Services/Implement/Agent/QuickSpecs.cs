using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-25.1 (D-143): bảng ánh xạ MỘT CHỖ – tên gợi ý trong <c>quick.yaml</c> (nhãn cột tiếng Việt, chỗ trống <c>{dem.x=…}</c> / <c>{tong.x}</c>…, bộ lọc <c>loc</c>)
    /// → trường thật của kết quả từng công cụ (JSON camelCase sau AgentMask). Nhãn không có dữ liệu ở công cụ → <see cref="Dropped"/> (bỏ cột, ghi PROGRESS).
    /// Test <c>QuickMapAi25Tests</c>: mọi cột / chỗ trống / bộ lọc của 72 câu đều ánh xạ được.
    /// </summary>
    public static class QuickSpecs
    {
        public delegate object? RowFn(JsonObject row, JsonNode? root);
        public delegate object? RootFn(JsonNode? root, IReadOnlyList<JsonObject> rows);

        public sealed class Spec
        {
            /// <summary>Dựng danh sách dòng từ kết quả (theo id câu khi 1 công cụ có nhiều kiểu dòng).</summary>
            public Func<JsonNode?, string, List<JsonObject>> Rows { get; init; } = (r, _) => Arr(r, "items");
            public Dictionary<string, RowFn> Cols { get; init; } = new(StringComparer.Ordinal);
            /// <summary>Trường dòng cho {dem.x=…} / {tong.x} / {tb.x} / {min.x} (khóa = tên gợi ý).</summary>
            public Dictionary<string, RowFn> Fields { get; init; } = new(StringComparer.Ordinal);
            /// <summary>Số cấp gốc cho {tong.x} / {pct.x} / {x} (ưu tiên hơn cộng dòng).</summary>
            public Dictionary<string, RootFn> Root { get; init; } = new(StringComparer.Ordinal);
        }

        // ---------------- tiện ích JSON
        public static string Fold(string? s)
        {
            string n = (s ?? string.Empty).Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormD);
            return new string(n.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant().Trim();
        }

        public static JsonNode? At(JsonNode? n, string path)
        {
            foreach (string p in path.Split('.'))
            {
                if (n is not JsonObject o || !o.TryGetPropertyValue(p, out n))
                {
                    return null;
                }
            }
            return n;
        }

        public static List<JsonObject> Arr(JsonNode? n, string path)
        {
            var a = path.Length == 0 ? n : At(n, path);
            if (a is JsonObject wrap && wrap["items"] is JsonArray inner)
            {
                a = inner; // mảng gốc dài bị bọc {items, total, truncated}
            }
            return a is JsonArray arr ? [.. arr.OfType<JsonObject>()] : [];
        }

        /// <summary>Làm phẳng mảng lồng: trường của cha chép vào con với tiền tố "^" (cấp gần nhất thắng).</summary>
        public static List<JsonObject> Flat(JsonNode? root, params string[] levels)
        {
            var cur = Arr(root, levels[0]).Select(x => (JsonObject)x.DeepClone()).ToList();
            foreach (string lv in levels.Skip(1))
            {
                var next = new List<JsonObject>();
                foreach (var parent in cur)
                {
                    foreach (var child in Arr(parent, lv))
                    {
                        var c = (JsonObject)child.DeepClone();
                        foreach (var kv in parent.Where(kv => kv.Value is not JsonArray && kv.Value is not JsonObject))
                        {
                            string k = kv.Key.StartsWith('^') ? kv.Key : "^" + kv.Key;
                            c.TryAdd(k, kv.Value?.DeepClone());
                        }
                        next.Add(c);
                    }
                }
                cur = next;
            }
            return cur;
        }

        /// <summary>Gộp nhiều danh sách, gắn nhãn vào trường "_tag".</summary>
        public static List<JsonObject> Tagged(JsonNode? root, params (string Path, string Tag)[] lists)
        {
            var all = new List<JsonObject>();
            foreach (var (path, tag) in lists)
            {
                foreach (var r in Arr(root, path))
                {
                    var c = (JsonObject)r.DeepClone();
                    c["_tag"] = tag;
                    all.Add(c);
                }
            }
            return all;
        }

        private static RowFn F(string path) => (r, _) => At(r, path)?.DeepClone();
        private static RowFn Tag => (r, _) => r["_tag"]?.GetValue<string>();
        private static RootFn R(string path) => (root, _) => At(root, path)?.DeepClone();
        private static decimal? D(JsonNode? n) => n is JsonValue v && v.TryGetValue(out decimal d) ? d : n is JsonValue s && s.TryGetValue(out string? t) && decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : null;
        public static decimal? Num(object? o) => o switch { null => null, decimal d => d, int i => i, double db => (decimal)db, JsonNode n => D(n), _ => null };
        private static bool Truthy(JsonNode? n) => n is JsonValue v && (v.TryGetValue(out bool b) ? b : v.TryGetValue(out decimal d) ? d != 0 : v.TryGetValue(out string? s) && !string.IsNullOrWhiteSpace(s) && s != "false");
        private static Dictionary<string, RowFn> M(params (string Label, RowFn Fn)[] x) => x.ToDictionary(a => a.Label, a => a.Fn, StringComparer.Ordinal);
        private static Dictionary<string, RootFn> MR(params (string Key, RootFn Fn)[] x) => x.ToDictionary(a => a.Key, a => a.Fn, StringComparer.Ordinal);

        private static string Sev(JsonNode? s) => Fold(s?.ToString()) switch
        {
            "high" or "critical" or "danger" or "error" or "cao" => "Cao",
            "medium" or "warn" or "warning" or "trung binh" => "Trung bình",
            _ => "Thấp",
        };

        // ---------------- nhãn không có dữ liệu (bỏ cột) – công cụ hiện không trả trường này
        public static readonly Dictionary<string, string[]> Dropped = new(StringComparer.Ordinal)
        {
            ["hop_khay"] = ["Phí dự kiến", "Quá hạn (ngày)"],
            ["khach_don_hang"] = ["Điểm giao", "Giờ giao dự kiến"],
            ["cham_cong_ngay"] = ["Điện thoại", "Đi muộn", "Vắng", "Về sớm"],
            ["tien_do_bep"] = ["Trễ (phút)"],
            ["thuc_don_tuan"] = ["Suất"],
            ["tim_tri_thuc"] = ["Màn"],
            ["chuyen_giao_ngay"] = ["Trễ (phút)"],
            ["canh_bao"] = ["Từ lúc", "Bộ phận"],
            ["ke_hoach_ngay"] = ["Thiếu nguyên liệu"],
            ["ke_hoach_mua"] = ["Đơn mua"],
            ["get_ton_kho"] = ["Tối thiểu", "Giá trị"],
                        ["don_hang_ngay"] = ["Người phụ trách"],
            ["kpi_khoang"] = ["Suất"],
        };

        public static readonly Dictionary<string, Spec> Tools = new(StringComparer.Ordinal)
        {
            ["chung_tu_ke_toan"] = new()
            {
                Rows = (r, _) => Arr(r, "listVoucher"),
                Cols = M(("Loại", F("typeName")), ("Số", F("number")), ("Ngày", F("date")), ("Giá trị", F("amount")), ("Trạng thái", F("statusName")), ("Lỗi", F("fixLabel"))),
                Fields = M(("loi", (r, _) => Fold(r["statusName"]?.ToString()) == "loi")),
            },
            ["doi_chieu_amis"] = new()
            {
                Rows = (r, _) => Arr(r, "listRow"),
                Cols = M(("Chỉ tiêu", F("title")), ("Delicare", F("delicare")), ("AMIS", F("amis")), ("Chênh lệch", F("difference"))),
                Fields = M(("khop", (r, _) => Fold(r["resultName"]?.ToString()) is var s && s.Contains("khop") && !s.Contains("khong"))),
            },
            ["gia_von_lai_gop"] = new()
            {
                Rows = (r, _) => Arr(r, "listRow"),
                Cols = M(("Khách / loại suất", (r, _) => $"{r["name"]}{(r["sub"] is { } s && s.ToString().Length > 0 ? " · " + s : string.Empty)}"), ("Doanh thu", F("revenue")), ("Giá vốn", F("cost")),
                    ("Lãi gộp %", (r, _) => Num(r["revenue"]) is { } rv && rv != 0 ? Math.Round((rv - (Num(r["cost"]) ?? 0)) / rv * 100, 1) : null), ("Giá vốn / suất", F("costPerMeal")), ("Suất", F("meals"))),
                Root = MR(("doanh_thu", R("summary.revenue")), ("gia_von", R("summary.cost")),
                    ("lai_gop_pct", (root, _) => Num(At(root, "summary.revenue")) is { } rv && rv != 0 ? Math.Round((rv - (Num(At(root, "summary.cost")) ?? 0)) / rv * 100, 1) : null)),
            },
            ["hop_khay"] = new()
            {
                Rows = (r, _) => Arr(r, "listItem"),
                Cols = M(("Khách", F("customerName")), ("Thất thoát", F("lossQuantity")), ("Ngưỡng hợp đồng", F("lossThresholdPct")), ("Đã giao", F("outQuantity")), ("Đã thu", F("inQuantity")),
                    ("Còn", F("outstanding")), ("Thất thoát %", F("lossRatePct"))),
                Fields = M(("vuot_nguong", F("isOverThreshold")), ("chua_thu", F("outstanding")), ("qua_han", (r, _) => (Num(r["overdueQuantity"]) ?? 0) > 0)),
            },
            ["ds_cho_duyet"] = new()
            {
                Rows = (r, _) => r is JsonArray ? Arr(r, string.Empty) : Arr(r, "items"),
                Cols = M(("Loại", F("summary.typeName")), ("Số chứng từ", (r, _) => At(r, "summary.documentNo")?.ToString() ?? r["requestNo"]?.ToString()), ("Giá trị", F("summary.mainValue")),
                    ("Hạn", F("dueAt")), ("Nhân viên", F("summary.counterparty")), ("Ngày", F("requestedAt")), ("Người gửi", F("requestedByName")), ("Gửi lúc", F("requestedAt"))),
                Fields = M(("qua_han", F("isOverdue"))),
            },
            ["khach_don_hang"] = new()
            {
                Rows = (r, _) => Flat(r is JsonArray ? new JsonObject { ["items"] = r.DeepClone() } : r, "items", "lines"),
                Cols = M(("Loại suất", F("mealType")), ("Số suất", F("quantity")), ("Trạng thái", F("^status"))),
                Fields = M(("suat", F("quantity"))),
                Root = MR(("trang_thai", (_, rows) => rows.FirstOrDefault()?["^status"]?.DeepClone())),
            },
            ["khach_thuc_don"] = new()
            {
                Rows = (r, _) => r is JsonArray ? Arr(r, string.Empty) : Arr(r, "items"),
                Cols = M(("Ngày", F("mealDate")), ("Buổi", F("mealType")), ("Món", (r, _) => r["dishes"] is JsonArray a ? string.Join(", ", a.Select(x => x?.ToString())) : null)),
            },
            ["cham_cong_ngay"] = new()
            {
                Rows = (r, q) => q is "hr-cham-cong" or "hr-hom-qua" ? Arr(r, "listShift")
                    : Tagged(r, ("listNotCheckedIn", "Chưa vào ca"), ("listCheckedIn", "Đã vào"), ("listOnLeave", "Nghỉ phép")),
                Cols = M(("Nhân viên", F("fullName")), ("Tổ", F("unitName")), ("Ca", (r, _) => r["shiftCode"]?.DeepClone() ?? r["name"]?.DeepClone()), ("Giờ vào ca", F("plannedStart")),
                    ("Giờ vào", F("checkInAt")), ("Muộn (phút)", F("lateMinutes")), ("Định biên", F("assigned")), ("Đã vào", F("present")),
                    ("Chưa vào", (r, _) => Num(r["assigned"]) is { } a ? a - (Num(r["present"]) ?? 0) : null)),
                Fields = M(("trang_thai", Tag), ("di_muon", (r, _) => (Num(r["lateMinutes"]) ?? 0) > 0), ("da_vao", F("present")), ("dinh_bien", F("assigned"))),
                Root = MR(("muon_phut", R("lateMinutesTotal")), ("can_xu_ly", R("needActionCount"))),
            },
            ["tien_do_bep"] = new()
            {
                Rows = (r, _) => Arr(r, "listBatch"),
                Cols = M(("Mẻ", F("batchNumber")), ("Món", F("dishName")), ("Dây chuyền", F("productionLineName")), ("Trạng thái", F("statusName")), ("Giờ xong dự kiến", F("plannedEnd")), ("Xong lúc", F("actualEnd"))),
                Fields = M(("trang_thai", F("statusName")), ("tre", (r, _) => Truthy(r["lateStart"]) || Truthy(r["lateFinish"]))),
            },
            ["tien_do_bep_chi_tiet"] = new()
            {
                Rows = (r, _) => Arr(r, "listTeam"),
                Cols = M(("Công đoạn", (r, _) => $"{r["stageName"]} · {r["teamName"]}"), ("Xong", F("done")), ("Tổng", (r, _) => (Num(r["waiting"]) ?? 0) + (Num(r["inProgress"]) ?? 0) + (Num(r["done"]) ?? 0)),
                    ("%", (r, _) => ((Num(r["waiting"]) ?? 0) + (Num(r["inProgress"]) ?? 0) + (Num(r["done"]) ?? 0)) is var t && t > 0 ? Math.Round((Num(r["done"]) ?? 0) / t * 100, 0) : null)),
                Root = MR(("so_che", StagePct("so che")), ("nau", StagePct("nau")), ("chia", StagePct("chia"))),
            },
            ["thuc_don_tuan"] = new()
            {
                Rows = (r, _) => Flat(r, "listMeal", "listOption", "listRow", "listCell"),
                Cols = M(("Buổi", F("^mealName")), ("Lựa chọn", F("^optionNo")), ("Món", F("dishName")), ("Ngày", (r, root) => MenuDay(r, root))),
                Fields = M(("ngay", (r, root) => MenuDay(r, root))),
                Root = MR(("fc", R("avgFcPct")), ("tuan", (root, _) => At(root, "weekStart")?.DeepClone())),
            },
            ["nhiet_do_ngay"] = new()
            {
                Rows = (r, _) => Arr(r, "listZone"),
                Cols = M(("Giờ", F("lastAt")), ("Điểm đo", F("zoneName")), ("Loại", F("measurementType")), ("Nhiệt độ", F("lastTemperature")),
                    ("Ngưỡng", (r, _) => $"{r["temperatureMin"]} – {r["temperatureMax"]} °C"), ("Kết quả", (r, _) => r["lastWithin"] is null ? "Chưa đo" : Truthy(r["lastWithin"]) ? "Đạt" : "Vượt ngưỡng")),
                Fields = M(("vuot", (r, _) => r["lastWithin"] is JsonValue v && v.TryGetValue(out bool b) && !b)),
                Root = MR(("thieu_lan_do", R("dueNotMeasured"))),
            },
            ["hao_hut_khoang"] = new()
            {
                Rows = (r, _) => Arr(r, "listItem"),
                Cols = M(("Loại", F("key")), ("Món", F("label")), ("Suất", F("quantityMeals")), ("%", (r, root) => Num(At(root, "totalMeals")) is { } t && t != 0 ? Math.Round((Num(r["quantityMeals"]) ?? 0) / t * 100, 1) : null)),
                Root = MR(("suat_huy", R("totalMeals")), ("kg", R("totalKg")), ("chi_phi", R("totalCost"))),
            },
            ["truy_xuat_me"] = Trace(),
            ["truy_xuat_lo"] = Trace(),
            ["tim_tri_thuc"] = new()
            {
                Rows = (r, _) => Arr(r, "ghi_chu"),
                Cols = M(("Tiêu đề", F("tieu_de")), ("Thư mục", F("thu_muc")), ("Hiệu lực từ", F("hieu_luc_tu")), ("Đoạn trích", F("doan_trich")), ("Mục", F("tieu_de")), ("Cập nhật", F("hieu_luc_tu"))),
            },
            ["chuyen_giao_ngay"] = new()
            {
                Rows = (r, q) => q == "log-chua-xep" ? Arr(r, "listUnassigned") : Arr(r, "listRoute"),
                Cols = M(("Chuyến", F("routeNo")), ("Xe", F("vehicleName")), ("Tài xế", F("driverName")),
                    ("Điểm giao", (r, _) => r["listPoint"] is JsonArray p ? $"{r["pointsComplete"]}/{p.Count}" : r["locationName"]?.DeepClone()), ("Giờ hẹn", (r, _) => r["plannedStart"]?.DeepClone() ?? r["plannedTime"]?.DeepClone()),
                    ("Trạng thái", F("statusName")), ("Khách", F("customerName")), ("Suất", F("meals"))),
                Fields = M(("trang_thai", F("statusName")), ("tre", (r, _) => RouteLate(r))),
                Root = MR(("don_chua_xep", (root, _) => Arr(root, "listUnassigned").Count)),
            },
            ["lo_ve_sinh"] = new()
            {
                Rows = (r, _) => Tagged(r, ("listWaiting", "Chờ làm"), ("listInProgress", "Đang làm"), ("listDone", "Hoàn thành")),
                Cols = M(("Lô", F("number")), ("Bước hiện tại", (r, _) => $"{r["passedStepCount"]}/{r["stepCount"]}"), ("Bắt đầu", F("startDate")), ("Kết quả", (r, _) => r["qcResult"]?.DeepClone() ?? r["_tag"]?.DeepClone()), ("Người làm", F("responsibleName"))),
                Fields = M(("trang_thai", Tag)),
            },
            ["canh_bao"] = new()
            {
                Rows = (r, _) => r is JsonArray ? Arr(r, string.Empty) : Arr(r, "items"),
                Cols = M(("Mức", (r, _) => Sev(r["severity"])), ("Nội dung", (r, _) => $"{r["title"]}{(r["detail"] is { } d && d.ToString().Length > 0 ? " – " + d : string.Empty)}"), ("Loại", F("code"))),
                Fields = M(("muc", (r, _) => Sev(r["severity"]))),
            },
            ["ke_hoach_ngay"] = new()
            {
                Rows = (r, _) => Arr(r, "listDay"),
                Cols = M(("Ngày", F("date")), ("Suất", (r, _) => (Num(r["lockedMeals"]) ?? 0) + (Num(r["openMeals"]) ?? 0)), ("Trạng thái", F("dayStatusName")), ("Bị giữ lại vì", F("heldReason")),
                    ("Lý do", F("heldReason")), ("Việc cần làm", F("action"))),
                Fields = M(("trang_thai", F("dayStatusName")), ("bi_giu", (r, _) => Truthy(r["heldReason"]))),
            },
            ["du_bao_thieu_hut"] = new()
            {
                Rows = (r, _) => Arr(r, "listItem"),
                Cols = M(("Nguyên liệu", F("ingredientName")), ("Ngày cần", F("firstShortageDate")), ("Thiếu", F("totalShortage")), ("Phải đặt trước", F("orderByDate")), ("Cần", F("totalRequired")),
                    ("Tồn lũy kế", F("availableQuantity")), ("Tồn", F("availableQuantity"))),
                Fields = M(("ngay_dat", F("orderByDate"))),
            },
            ["ke_hoach_mua"] = new()
            {
                Rows = (r, _) => Arr(r, "viecCanLam"),
                Cols = M(("Nguyên liệu", F("ingredientName")), ("Ngày cần", F("date")), ("Số lượng", (r, _) => $"{r["quantity"]} {r["unitName"]}".Trim()), ("NCC gợi ý", F("sub")), ("NCC", F("sub")), ("Trạng thái", F("statusName"))),
                Fields = M(("nhom", F("statusName"))),
                Root = MR(("phai_dat", R("kpi.dueToday")), ("qua_han", R("kpi.late")), ("cho_duyet", R("kpi.pendingApproval")), ("chua_gui", R("kpi.unsentPurchaseOrder"))),
            },
            ["get_ton_kho"] = new()
            {
                Rows = (r, _) => Arr(r, "items"),
                Cols = M(("Nguyên liệu", F("ten")), ("Tồn", F("ton")), ("Đơn vị", F("dvt")), ("Kho", F("kho"))),
            },
            ["su_co_ngay"] = new()
            {
                Rows = (r, _) =>
                {
                    var rows = new List<JsonObject>();
                    rows.AddRange(Arr(r, "canh_bao").Select(a => new JsonObject { ["loai"] = "Cảnh báo", ["noi_dung"] = $"{a["title"]}{(a["detail"] is { } d && d.ToString().Length > 0 ? " – " + d : string.Empty)}", ["muc"] = Sev(a["severity"]), ["trang_thai"] = "Đang mở" }));
                    rows.AddRange(Arr(r, "nhiet_do.listZone").Where(z => z["lastWithin"] is JsonValue v && v.TryGetValue(out bool b) && !b)
                        .Select(z => new JsonObject { ["gio"] = z["lastAt"]?.DeepClone(), ["loai"] = "Nhiệt độ", ["noi_dung"] = $"{z["zoneName"]}: {z["lastTemperature"]} °C (ngưỡng {z["temperatureMin"]} – {z["temperatureMax"]})", ["muc"] = "Cao", ["trang_thai"] = z["status"]?.DeepClone() }));
                    rows.AddRange(Arr(r, "phan_anh.listItem").Select(p => new JsonObject { ["loai"] = "Phản ánh", ["noi_dung"] = $"{p["complaint"]} · {p["customerName"]}", ["muc"] = p["severityName"]?.DeepClone(), ["trang_thai"] = p["statusName"]?.DeepClone() }));
                    return rows;
                },
                Cols = M(("Giờ", F("gio")), ("Loại", F("loai")), ("Nội dung", F("noi_dung")), ("Mức", F("muc")), ("Trạng thái", F("trang_thai"))),
                Fields = M(("loai", F("loai"))),
            },
            ["get_lo_can_date"] = new()
            {
                Rows = (r, _) => r is JsonArray ? Arr(r, string.Empty) : Arr(r, "items"),
                Cols = M(("Nguyên liệu", F("ingredientName")), ("Số lô", (r, _) => At(r, "lot.lotNumber")?.DeepClone() ?? At(r, "lot.lotNo")?.DeepClone()), ("Hạn dùng", (r, _) => At(r, "lot.expiryDate")?.DeepClone()),
                    ("Trạng thái QC", (r, _) => Truthy(r["isBlocked"]) ? "Tạm giữ" : Truthy(r["isExpired"]) ? "Hết hạn" : At(r, "lot.qcStatus")?.DeepClone() ?? "Đạt"), ("Còn (ngày)", F("daysToExpiry")), ("Tồn", F("onHandQuantity"))),
            },
            ["phan_anh_khach"] = new()
            {
                Rows = (r, _) => Arr(r, "listItem"),
                Cols = M(("Số", F("complaint.complaintNo")), ("Nội dung", F("complaint.description")), ("Tiếp nhận", F("complaint.receivedAt")), ("Khách", F("customerName")), ("Hạn", (r, _) => Num(r["hoursToDue"]) is { } h ? (h < 0 ? $"quá {Math.Round(-h, 0)} giờ" : $"còn {Math.Round(h, 0)} giờ") : null), ("Người xử lý", F("assignedToName"))),
                Fields = M(("qua_han", F("isOverdue"))),
            },
            ["don_hang_ngay"] = new()
            {
                Rows = (r, _) => Flat(r, "listCustomer", "listRow"),
                Cols = M(("Khách", F("^customerName")), ("Điểm giao", F("locationName")), ("Loại suất", (r, _) => r["listMeal"] is JsonArray a ? string.Join(", ", a.OfType<JsonObject>().Where(m => (Num(m["quantity"]) ?? 0) > 0).Select(m => $"{m["mealType"]} {m["quantity"]}")) : null),
                    ("Số suất", F("total")), ("Số suất dự kiến", F("forecast")), ("Trạng thái", (r, _) => r["statusName"]?.DeepClone() ?? "Chưa chốt")),
                Fields = M(("suat", F("total")), ("trang_thai", (r, _) => r["orderId"] is null ? "Chưa chốt" : r["statusName"]?.ToString())),
                Root = MR(("khach", R("customerCount")), ("suat", R("totalQuantity"))),
            },
            ["bao_cao_dieu_hanh"] = new()
            {
                Rows = (r, _) => [new JsonObject
                {
                    ["don"] = $"{At(r, "kpi.orders.total")} đơn / {At(r, "kpi.plannedMeals")} suất", ["sx"] = At(r, "kpi.productionProgress.percent") is { } pp ? $"{pp}%" : "–",
                    ["giao"] = $"{At(r, "kpi.delivery.delivered")}/{At(r, "kpi.delivery.total")}", ["cl"] = $"{At(r, "kpi.qcHoldReject")} giữ / loại", ["kho"] = $"{At(r, "kpi.stockAlerts")} cảnh báo",
                    ["cb"] = Arr(r, "alerts").FirstOrDefault()?["title"]?.DeepClone(),
                }],
                Cols = M(("Đơn / suất", F("don")), ("Sản xuất", F("sx")), ("Giao hàng", F("giao")), ("Chất lượng", F("cl")), ("Kho", F("kho")), ("Cảnh báo chính", F("cb"))),
                Root = MR(("suat", R("kpi.plannedMeals")), ("sx_pct", R("kpi.productionProgress.percent")), ("me", R("kpi.productionProgress.batchCount")), ("chuyen_xong", R("kpi.delivery.delivered")), ("chuyen", R("kpi.delivery.total")),
                    ("canh_bao", (root, _) => Arr(root, "alerts").Count)),
            },
            ["kpi_khoang"] = new()
            {
                Rows = (r, _) =>
                {
                    var metrics = Arr(r, "metrics");
                    JsonObject? Pick(string kw) => metrics.FirstOrDefault(m => Fold(At(m, "definition.metricName")?.ToString()).Contains(kw));
                    var cols = new (string Key, JsonObject? M)[] { ("dung_gio", Pick("dung gio")), ("hao_hut", Pick("hao hut")), ("phan_anh", Pick("phan anh")) };
                    var dates = metrics.SelectMany(m => Arr(m, "series")).Select(s => s["date"]?.ToString()).Where(d => d != null).Distinct().OrderBy(d => d).ToList();
                    return [.. dates.Select(d =>
                    {
                        var row = new JsonObject { ["date"] = d };
                        foreach (var (key, m) in cols)
                        {
                            row[key] = m == null ? null : Arr(m, "series").FirstOrDefault(s => s["date"]?.ToString() == d)?["value"]?.DeepClone();
                        }
                        return row;
                    })];
                },
                Cols = M(("Ngày", F("date")), ("Giao đúng giờ %", F("dung_gio")), ("Hao hụt %", F("hao_hut")), ("Phản ánh", F("phan_anh"))),
                Fields = M(("dung_gio", F("dung_gio")), ("hao_hut", F("hao_hut"))),
            },
        };

        private static Spec Trace() => new()
        {
            Rows = (r, _) => Tagged(r, ("listSupplier", "NCC"), ("listPurchaseOrder", "Đơn mua"), ("listGoodsReceipt", "Phiếu nhập"), ("listLot", "Lô"), ("listIssue", "Phiếu xuất"),
                ("listProductionOrder", "Lệnh sản xuất"), ("listBatch", "Mẻ nấu"), ("listPackingOrder", "Đóng gói"), ("listOrder", "Đơn hàng"), ("listDeliveryOrder", "Giao hàng"), ("listCustomer", "Khách")),
            Cols = M(("Bước", Tag), ("Chứng từ", F("number")), ("Ngày", F("date")), ("Đối tượng", F("name")), ("Số lượng", F("quantity"))),
            Fields = M(("buoc", Tag)),
            Root = MR(("nha_cung_cap", (root, _) => Arr(root, "listSupplier").FirstOrDefault()?["name"]?.DeepClone())),
        };

        private static RootFn StagePct(string keyword) => (root, _) =>
        {
            var teams = Arr(root, "listTeam").Where(t => Fold(t["stageName"]?.ToString()).Contains(keyword)).ToList();
            decimal total = teams.Sum(t => (Num(t["waiting"]) ?? 0) + (Num(t["inProgress"]) ?? 0) + (Num(t["done"]) ?? 0)), done = teams.Sum(t => Num(t["done"]) ?? 0);
            return total == 0 ? 0 : Math.Round(done / total * 100, 0);
        };

        private static string? MenuDay(JsonObject r, JsonNode? root)
        {
            var day = r["day"];
            if (day is JsonValue v && v.TryGetValue(out int i) && At(root, "listDate") is JsonArray dates && i >= 0 && i < dates.Count)
            {
                return dates[i]?.ToString()[..10];
            }
            return day?.ToString() is { Length: >= 10 } s ? s[..10] : day?.ToString();
        }

        private static bool RouteLate(JsonObject r)
            => !Fold(r["statusName"]?.ToString()).Contains("hoan thanh") && !Fold(r["statusName"]?.ToString()).Contains("da giao")
               && DateTime.TryParse(r["plannedStart"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) && t < DateTime.Now && Num(r["pointsComplete"]) is { } pc && r["listPoint"] is JsonArray p && pc < p.Count;

        /// <summary>17 bộ lọc <c>loc</c> – theo id câu (mô tả tiếng Việt trong quick.yaml → điều kiện trên trường thật).</summary>
        public static readonly Dictionary<string, Func<JsonObject, bool>> Filters = new(StringComparer.Ordinal)
        {
            ["acc-chung-tu"] = r => !Fold(r["statusName"]?.ToString()).Contains("da gui amis"),
            ["hr-chua-vao"] = r => r["_tag"]?.ToString() == "Chưa vào ca",
            ["hr-di-muon"] = r => (Num(r["lateMinutes"]) ?? 0) > 0,
            ["kit-me-tre"] = r => Truthy(r["lateStart"]) || Truthy(r["lateFinish"]),
            ["kit-hom-nay"] = r => true, // xử lý ở QuickEngine (cần ngày gốc)
            ["kit-nhiet-do"] = r => r["lastWithin"] is JsonValue v && v.TryGetValue(out bool b) && !b,
            ["log-chua-xep"] = r => true, // dòng đã là listUnassigned
            ["log-tre"] = RouteLate,
            ["log-khay"] = r => (Num(r["outstanding"]) ?? 0) > 0,
            ["pkg-qua-han"] = r => (Num(r["overdueQuantity"]) ?? 0) > 0,
            ["pkg-canh-bao"] = r => Regex.IsMatch(r["code"]?.ToString() ?? string.Empty, "CONTAINER|PACKAG|CLEAN|KHAY|TRAY", RegexOptions.IgnoreCase),
            ["plan-bi-giu"] = r => Truthy(r["heldReason"]),
            ["proc-dat-hom-nay"] = r => Fold(r["statusName"]?.ToString()) is var s && (s.Contains("hom nay") || s.Contains("qua han") || s.Contains("tre")),
            ["proc-chua-gui"] = r => Fold(r["statusName"]?.ToString()) is var s && (s.Contains("chua gui") || s.Contains("da duyet")),
            ["qa-cho-qc"] = r => Fold(r["statusName"]?.ToString()).Contains("qc") || Fold(r["qcStatus"]?.ToString()).Contains("pending"),
            ["qa-ve-sinh"] = r => r["_tag"]?.ToString() != "Hoàn thành",
            ["sales-chua-chot"] = r => r["orderId"] is null || Fold(r["statusName"]?.ToString()).Contains("nhap"),
        };
    }
}
