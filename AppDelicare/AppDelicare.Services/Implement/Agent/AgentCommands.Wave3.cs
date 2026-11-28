using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-16 (D-133 đợt 3): lệnh ghi 2 pha của PLAN, PKG, ACC. Như AI-10: mỗi lệnh = ĐÚNG MỘT thao tác web (cùng method Service, cùng màn + thao tác người duyệt phải có);
    /// đề xuất chỉ kiểm + xem trước (không ghi), duyệt → method chạy dưới người duyệt.
    /// </summary>
    public static partial class AgentCommandRegistry
    {
        public static readonly string[] MenuChangeReasons = ["SHORTAGE", "PRICE", "SUPPLIER", "QUALITY", "OTHER"];

        private static async Task<AppDelicare.Model.Entity.Customer> CustomerAsync(IServiceProvider sp, JsonElement a)
        {
            string code = Need(a, "ma_khach", "mã khách hàng");
            return await Uow(sp).DbContext.Customer.AsNoTracking().FirstOrDefaultAsync(c => !c.IsDeleted && (c.Code == code || c.CIF == code)) ?? throw new AgentToolException($"Không có khách hàng «{code}»");
        }

        private static async Task<AppDelicare.Model.Entity.Dish> DishAsync(IServiceProvider sp, JsonElement a, string key, string label)
        {
            string code = Need(a, key, label);
            return await Uow(sp).DbContext.Dish.AsNoTracking().FirstOrDefaultAsync(d => !d.IsDeleted && d.Code == code) ?? throw new AgentToolException($"Không có món «{code}»");
        }

        private static int Int(JsonElement a, string key, int fallback) => Dec(a, key) is { } d ? (int)d : fallback;

        private static IEnumerable<AgentCommandDef> Wave3() =>
        [
            // ------------------------------------------------------------ PLAN – kế hoạch & thực đơn
            new AgentCommandDef
            {
                Code = "propose_production_plan", Title = "Tính lại kế hoạch sản xuất ngày", Agents = ["PLAN"], Screen = "prod-plan", Action = PermissionAction.Edit,
                Description = "Đề xuất TÍNH LẠI kế hoạch sản xuất 1 ngày (như nút Tính lại kế hoạch trên màn Kế hoạch 7 ngày tới): lấy đơn đã chốt, áp đổi món đã duyệt; hết đơn mở + bật tự phát lệnh "
                    + "thì tự duyệt / phát lệnh hoặc giữ lại kèm lý do như web. Người có quyền Sửa Kế hoạch sản xuất xác nhận.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ngay\":{\"type\":\"string\",\"description\":\"Ngày sản xuất yyyy-MM-dd\"}," + Common + "},\"required\":[\"ngay\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    DateTime day = Day(a, "ngay") ?? throw new AgentToolException("Thiếu ngày sản xuất (ngay)");
                    var days = await Svc<IProductionPlanService>(sp).GetDays(new ProductionPlanDaysRequest { From = day, To = day });
                    var d = days.ResponseCode == 200 ? days.Data?.ListDay.FirstOrDefault(x => x.Date.Date == day) : null;
                    if (d == null || d.LockedOrderCount == 0)
                    {
                        throw new AgentToolException($"Ngày {day:dd/MM/yyyy} chưa có đơn đã chốt – chưa tính kế hoạch được");
                    }
                    var pv = new AgentPreview
                    {
                        Summary = $"Tính lại kế hoạch sản xuất ngày {day:dd/MM/yyyy}: {d.LockedOrderCount} đơn đã chốt ({d.LockedMeals:0.#} suất)",
                        DocumentNo = d.PlanNumber, MainValue = $"{d.LockedMeals:0.#} suất", OpenLink = "#/prod-plan",
                        LineHeaders = ["Kế hoạch", "Trạng thái", "Đơn chốt", "Đơn còn mở"],
                        Lines = [[d.PlanNumber ?? "(chưa có)", d.DayStatusName, $"{d.LockedOrderCount}", $"{d.OpenOrderCount}"]],
                        Payload = new JsonObject { ["day"] = day.ToString("yyyy-MM-dd"), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                    if (d.OpenOrderCount > 0)
                    {
                        pv.Warnings.Add($"Còn {d.OpenOrderCount} đơn chưa chốt ({d.OpenMeals:0.#} suất) – kế hoạch chỉ tính đơn đã chốt");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IProductionPlanService>(sp).Recalculate(new ProductionPlanRecalculateRequest { ProductionDate = DateTime.Parse(S(p, "day"), System.Globalization.CultureInfo.InvariantCulture) });
                    return r.ResponseCode == 200 ? (null, r.Data?.Message is { Length: > 0 } m ? m : $"Đã tính lại kế hoạch {r.Data?.PlanNumber}") : (r.ResponseMessage ?? "Không tính lại được kế hoạch", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_menu_change", Title = "Đổi món trên thực đơn tuần đã công bố", Agents = ["PLAN"], Screen = "menu-menu", Action = PermissionAction.Edit,
                Description = "Đề xuất ĐỔI 1 món ở 1 ô thực đơn tuần đã công bố / đã duyệt (như nút Đổi món trên màn Thực đơn tuần): ngày, buổi, lựa chọn, món cũ → món mới, mã lý do. "
                    + "Sau khi xác nhận, yêu cầu đổi món đi tiếp như web: đủ 4 điều kiện (cùng nhóm, công thức khóa, food cost trong ngưỡng, trước giờ chốt) thì tự duyệt, còn lại chờ duyệt đổi món. "
                    + "Thực đơn còn Nháp → sửa trực tiếp trên web, không qua lệnh này.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ngay\":{\"type\":\"string\",\"description\":\"Ngày ăn yyyy-MM-dd\"},\"buoi\":{\"type\":\"string\",\"description\":\"Mã buổi (vd TRUA) hoặc tên buổi\"},"
                    + "\"lua_chon\":{\"type\":\"integer\",\"description\":\"Số lựa chọn (mặc định 1)\"},\"mon_cu\":{\"type\":\"string\",\"description\":\"Mã món đang có\"},\"mon_moi\":{\"type\":\"string\",\"description\":\"Mã món thay\"},"
                    + "\"ma_ly_do\":{\"type\":\"string\",\"enum\":[\"SHORTAGE\",\"PRICE\",\"SUPPLIER\",\"QUALITY\",\"OTHER\"]}," + Common + "},\"required\":[\"ngay\",\"buoi\",\"mon_cu\",\"mon_moi\",\"ma_ly_do\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    DateTime day = Day(a, "ngay") ?? throw new AgentToolException("Thiếu ngày ăn (ngay)");
                    string meal = Need(a, "buoi", "buổi");
                    int option = Math.Max(1, Int(a, "lua_chon", 1));
                    string reasonCode = (Need(a, "ma_ly_do", "mã lý do")).ToUpperInvariant();
                    if (!MenuChangeReasons.Contains(reasonCode))
                    {
                        throw new AgentToolException("Mã lý do phải là SHORTAGE / PRICE / SUPPLIER / QUALITY / OTHER");
                    }
                    var oldDish = await DishAsync(sp, a, "mon_cu", "món đang có");
                    var newDish = await DishAsync(sp, a, "mon_moi", "món thay");
                    if (oldDish.Id == newDish.Id)
                    {
                        throw new AgentToolException("Món thay trùng món đang có");
                    }
                    var weekResult = await Svc<IMenuService>(sp).GetWeek(new MenuWeekRequest { WeekStart = day });
                    var week = weekResult.ResponseCode == 200 && weekResult.Data?.MenuId != null ? weekResult.Data : throw new AgentToolException($"Tuần có ngày {day:dd/MM/yyyy} chưa có thực đơn");
                    if (week.Editable)
                    {
                        throw new AgentToolException($"Thực đơn {week.MenuCode} đang {week.StatusName} – sửa trực tiếp ô trên màn Thực đơn tuần, không cần đề xuất đổi món");
                    }
                    int dayNo = week.ListDate.FindIndex(x => x.Date == day) + 1;
                    var mealModel = week.ListMeal.FirstOrDefault(m => string.Equals(m.MealType, meal, StringComparison.OrdinalIgnoreCase) || string.Equals(m.MealName, meal, StringComparison.OrdinalIgnoreCase))
                        ?? throw new AgentToolException($"Thực đơn tuần không có buổi «{meal}» (có: {string.Join(", ", week.ListMeal.Select(m => m.MealType))})");
                    bool inCell = dayNo > 0 && mealModel.ListOption.Where(o => o.OptionNo == option).SelectMany(o => o.ListRow).SelectMany(r => r.ListCell).Any(c => c.Day == dayNo && c.DishId == oldDish.Id);
                    if (!inCell)
                    {
                        throw new AgentToolException($"Ngày {day:dd/MM/yyyy} buổi {mealModel.MealName} lựa chọn {option} không có món {oldDish.Code} {oldDish.Name}");
                    }
                    var req = new MenuChangeCellRequest
                    {
                        MenuId = week.MenuId!.Value, WeekStart = week.WeekStart, MealType = mealModel.MealType, OptionNo = option, Day = dayNo, OriginalDishId = oldDish.Id, RequestedDishId = newDish.Id,
                        ReasonCode = reasonCode, Note = AgentToolRegistry.Str(a, "ly_do"),
                    };
                    var prev = await Svc<IMenuChangeRequestService>(sp).PreviewCell(req);
                    if (prev.ResponseCode != 200 || prev.Data == null)
                    {
                        throw new AgentToolException(prev.ResponseMessage ?? "Không xem trước được đổi món");
                    }
                    var x = prev.Data;
                    var pv = new AgentPreview
                    {
                        Summary = $"Đổi món {oldDish.Name} → {newDish.Name} ngày {day:dd/MM/yyyy} buổi {mealModel.MealName} (lựa chọn {option}): {x.Customers} khách, {x.Orders} đơn, {x.Quantity:0.#} suất"
                            + (x.AutoApprove ? " – đủ điều kiện tự duyệt" : " – sẽ chờ duyệt đổi món"),
                        DocumentNo = week.MenuCode, MainValue = $"{x.Quantity:0.#} suất", OpenLink = "#/menu-menu",
                        LineHeaders = ["Điều kiện", "Đạt"],
                        Lines = [["Cùng nhóm món", x.SameCategory ? "Có" : "Không"], ["Công thức đã khóa", x.HasRecipe ? "Có" : "Không"], ["Food cost trong ngưỡng", x.FcPct == null ? "chưa có giá" : $"{x.FcPct:0.#}% – {(x.FcOk ? "Có" : "Không")}"],
                            ["Trước giờ chốt", $"{(x.BeforeCutoff ? "Có" : "Không")} (chốt {x.CutoffAt:dd/MM HH:mm})"]],
                        Payload = JsonSerializer.SerializeToNode(req)!.AsObject(),
                    };
                    pv.Warnings.AddRange(x.ListBlock);
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var req = p.Deserialize<MenuChangeCellRequest>() ?? throw new AgentToolException("Đề xuất hỏng");
                    var r = await Svc<IMenuChangeRequestService>(sp).CreateForCell(req);
                    return r.ResponseCode == 200 ? (null, r.ResponseMessage ?? "Đã tạo yêu cầu đổi món") : (r.ResponseMessage ?? "Không tạo được yêu cầu đổi món", null);
                },
            },
            // ------------------------------------------------------------ PKG – hộp khay & vệ sinh
            new AgentCommandDef
            {
                Code = "propose_container_return", Title = "Ghi khách trả hộp / khay", Agents = ["PKG", "LOG"], Screen = "log-packaging-ledger", Action = PermissionAction.Add,
                Description = "Đề xuất GHI khách trả hộp / khay theo 1 phiếu giao đã giao (như Ghi trả trên màn Sổ hộp khay): số trả, số hỏng, loại hộp. Không vượt số khách đang giữ. "
                    + "Người có quyền Thêm Sổ hộp khay (giao vận / điểm phát) xác nhận; sau đó QC kiểm hộp trả như web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_phieu_giao\":{\"type\":\"string\"},\"so_tra\":{\"type\":\"integer\"},\"so_hong\":{\"type\":\"integer\"},\"loai_hop\":{\"type\":\"string\",\"description\":\"Trống = theo phiếu giao\"},"
                    + "\"ngay\":{\"type\":\"string\"}," + Common + "},\"required\":[\"so_phieu_giao\",\"so_tra\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_phieu_giao", "số phiếu giao");
                    var dlv = await (from d in db.DeliveryOrder.AsNoTracking() join c in db.Customer.AsNoTracking() on d.CustomerId equals c.Id
                                     where !d.IsDeleted && d.DeliveryNo == no select new { d.Id, d.Status, d.CustomerId, c.Code, c.Name }).FirstOrDefaultAsync() ?? throw new AgentToolException($"Không có phiếu giao «{no}»");
                    int returned = Int(a, "so_tra", 0), damaged = Int(a, "so_hong", 0);
                    if (returned <= 0 || damaged < 0 || damaged > returned)
                    {
                        throw new AgentToolException("Số trả phải > 0 và số hỏng không quá số trả");
                    }
                    string? type = AgentToolRegistry.Str(a, "loai_hop");
                    var bal = await Svc<IPackagingLedgerService>(sp).GetBalance(new PackagingBalanceRequest { CustomerId = dlv.CustomerId, PackagingType = type });
                    decimal holding = bal.Data?.ListItem.Sum(i => i.Outstanding) ?? 0;
                    var pv = new AgentPreview
                    {
                        Summary = $"{dlv.Name} trả {returned} hộp / khay theo phiếu giao {no}" + (damaged > 0 ? $" ({damaged} hỏng)" : string.Empty),
                        DocumentNo = no, Counterparty = $"{dlv.Code} {dlv.Name}", MainValue = $"{returned}", OpenLink = "#/log-packaging-ledger",
                        LineHeaders = ["Loại", "Khách đang giữ", "Trả", "Hỏng"], Lines = [[type ?? "theo phiếu giao", $"{holding:0.#}", $"{returned}", $"{damaged}"]],
                        Payload = new JsonObject
                        {
                            ["deliveryId"] = dlv.Id.ToString(), ["returned"] = returned, ["damaged"] = damaged, ["type"] = type, ["day"] = Day(a, "ngay")?.ToString("yyyy-MM-dd"), ["reason"] = AgentToolRegistry.Str(a, "ly_do"),
                        },
                    };
                    if (returned > holding)
                    {
                        pv.Warnings.Add($"Số trả {returned} > số khách đang giữ {holding:0.#} – xác nhận sẽ bị từ chối");
                    }
                    if (dlv.Status != DeliveryOrderStatus.Delivered)
                    {
                        pv.Warnings.Add($"Phiếu giao đang {dlv.Status} – chỉ ghi trả được khi đã giao");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IContainerReturnService>(sp).Record(new ContainerReturnRecordRequest
                    {
                        DeliveryOrderId = G(p, "deliveryId"), ReturnedQuantity = p["returned"]!.GetValue<int>(), DamagedQuantity = p["damaged"]?.GetValue<int>(), PackagingType = p["type"]?.GetValue<string?>(),
                        ReturnDate = p["day"]?.GetValue<string?>() is { } d ? DateTime.Parse(d, System.Globalization.CultureInfo.InvariantCulture) : null, Note = $"[Agent] {S(p, "reason")}",
                    });
                    return r.ResponseCode == 200 ? (null, $"Đã ghi trả hộp – phiếu giao {r.Data?.DeliveryNumber}") : (r.ResponseMessage ?? "Không ghi được trả hộp", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_cleaning_step", Title = "Ghi kết quả 1 bước vệ sinh khay", Agents = ["PKG"], Screen = "qc-cleaning", Action = PermissionAction.Add,
                Description = "Đề xuất GHI kết quả 1 bước của lô vệ sinh khay (quy trình VS-KHAY 6 bước, như màn Vệ sinh hộp / khay): mã bước, PASS / FAIL, hóa chất (bước bắt buộc hóa chất), "
                    + "biện pháp khắc phục (FAIL). Bước trước bắt buộc phải PASS. Tổ trưởng vệ sinh / QA (quyền Thêm màn Vệ sinh) xác nhận.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_lo_ve_sinh\":{\"type\":\"string\",\"description\":\"Số lô vệ sinh VS-…\"},\"ma_buoc\":{\"type\":\"string\"},\"ket_qua\":{\"type\":\"string\",\"enum\":[\"PASS\",\"FAIL\"]},"
                    + "\"gia_tri\":{\"type\":\"string\"},\"hoa_chat\":{\"type\":\"string\"},\"khac_phuc\":{\"type\":\"string\"}," + Common + "},\"required\":[\"so_lo_ve_sinh\",\"ma_buoc\",\"ket_qua\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_lo_ve_sinh", "số lô vệ sinh");
                    var id = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && (b.CleaningBatchNumber == no || b.CleaningNo == no)).Select(b => (Guid?)b.Id).FirstOrDefaultAsync()
                        ?? throw new AgentToolException($"Không có lô vệ sinh «{no}»");
                    var det = await Svc<ICleaningBatchService>(sp).GetDetail(id);
                    var batch = det.ResponseCode == 200 && det.Data != null ? det.Data : throw new AgentToolException(det.ResponseMessage ?? "Không đọc được lô vệ sinh");
                    if (batch.Batch.Status != CleaningBatchStatus.InProgress)
                    {
                        throw new AgentToolException($"Lô {no} đang {batch.StatusName} – không ghi thêm bước được");
                    }
                    string step = Need(a, "ma_buoc", "mã bước");
                    string result = Need(a, "ket_qua", "kết quả").ToUpperInvariant();
                    if (result is not ("PASS" or "FAIL"))
                    {
                        throw new AgentToolException("Kết quả phải là PASS hoặc FAIL");
                    }
                    int idx = batch.ListStep.FindIndex(s => string.Equals(s.Item.ItemCode, step, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0)
                    {
                        throw new AgentToolException($"Quy trình {batch.ChecklistCode} không có bước «{step}» (có: {string.Join(", ", batch.ListStep.Select(s => s.Item.ItemCode))})");
                    }
                    var cur = batch.ListStep[idx];
                    var notPassed = batch.ListStep.Take(idx).Where(s => s.Item.IsRequired && s.Result?.ResultCode != "PASS").Select(s => s.Item.ItemCode).ToList();
                    if (notPassed.Count > 0)
                    {
                        throw new AgentToolException($"Bước trước chưa PASS: {string.Join(", ", notPassed)} – ghi theo thứ tự");
                    }
                    string? chem = AgentToolRegistry.Str(a, "hoa_chat"), fix = AgentToolRegistry.Str(a, "khac_phuc");
                    if (result == "FAIL" && fix == null)
                    {
                        throw new AgentToolException("Bước FAIL phải có biện pháp khắc phục (khac_phuc)");
                    }
                    if (result == "PASS" && cur.RequiresChemical && chem == null)
                    {
                        throw new AgentToolException($"Bước {step} bắt buộc ghi hóa chất (hoa_chat)" + (batch.SuggestedChemical is { } sc ? $" – gợi ý {sc}" : string.Empty));
                    }
                    return new AgentPreview
                    {
                        Summary = $"Lô vệ sinh {no} ({batch.CustomerName}) – bước {idx + 1}/{batch.StepCount} {cur.Item.ItemName}: {result}",
                        DocumentNo = no, Counterparty = batch.CustomerName, MainValue = result, OpenLink = "#/qc-cleaning?id=" + id,
                        LineHeaders = ["Bước", "Kết quả", "Giá trị", "Hóa chất", "Khắc phục"], Lines = [[$"{cur.Item.ItemCode} {cur.Item.ItemName}", result, AgentToolRegistry.Str(a, "gia_tri"), chem, fix]],
                        Payload = new JsonObject
                        {
                            ["batchId"] = id.ToString(), ["itemId"] = cur.Item.Id.ToString(), ["checklistId"] = batch.ChecklistId?.ToString(), ["result"] = result, ["value"] = AgentToolRegistry.Str(a, "gia_tri"),
                            ["chem"] = chem, ["fix"] = fix,
                        },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<ICleaningBatchService>(sp).RecordResults(new CleaningRecordRequest
                    {
                        Id = G(p, "batchId"), ChecklistId = p["checklistId"]?.GetValue<string?>() is { } c ? Guid.Parse(c) : null,
                        ListItem = [new CleaningRecordItemRequest { ChecklistItemId = G(p, "itemId"), ResultCode = S(p, "result"), ActualValue = p["value"]?.GetValue<string?>(), ChemicalUsed = p["chem"]?.GetValue<string?>(), CorrectiveAction = p["fix"]?.GetValue<string?>() }],
                    });
                    return r.ResponseCode == 200 ? (null, "Đã ghi kết quả bước vệ sinh") : (r.ResponseMessage ?? "Không ghi được bước vệ sinh", null);
                },
            },
            // ------------------------------------------------------------ ACC / PKG – hộp thất thoát
            new AgentCommandDef
            {
                Code = "propose_container_loss_charge", Title = "Ghi hộp / khay thất thoát (tính phí khách)", Agents = ["ACC", "PKG"], Screen = "log-packaging-ledger", Action = PermissionAction.Add,
                Description = "Đề xuất GHI THẤT THOÁT hộp / khay của khách (như Ghi thất thoát trên màn Sổ hộp khay) – số thất thoát vào bảng kê công nợ kỳ đó và được tính phí theo "
                    + "phí thất thoát / cái của danh mục Loại hộp / khay khi vượt ngưỡng (như Bảng kê thanh toán). Không vượt số khách đang giữ. Người có quyền Thêm Sổ hộp khay xác nhận.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ma_khach\":{\"type\":\"string\"},\"loai_hop\":{\"type\":\"string\"},\"so_luong\":{\"type\":\"number\"},\"so_phieu_giao\":{\"type\":\"string\"}," + Common + "},"
                    + "\"required\":[\"ma_khach\",\"loai_hop\",\"so_luong\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    var cus = await CustomerAsync(sp, a);
                    string type = Need(a, "loai_hop", "loại hộp");
                    decimal qty = Dec(a, "so_luong") is > 0 and var q ? q : throw new AgentToolException("Số thất thoát phải > 0");
                    string? dlvNo = AgentToolRegistry.Str(a, "so_phieu_giao");
                    Guid? dlvId = dlvNo == null ? null : await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.DeliveryNo == dlvNo && d.CustomerId == cus.Id).Select(d => (Guid?)d.Id).FirstOrDefaultAsync()
                        ?? throw new AgentToolException($"Khách {cus.Code} không có phiếu giao «{dlvNo}»");
                    var bal = await Svc<IPackagingLedgerService>(sp).GetBalance(new PackagingBalanceRequest { CustomerId = cus.Id, PackagingType = type });
                    var item = bal.Data?.ListItem.FirstOrDefault(i => string.Equals(i.PackagingType, type, StringComparison.OrdinalIgnoreCase));
                    decimal holding = item?.Outstanding ?? 0;
                    if (qty > holding)
                    {
                        throw new AgentToolException($"Khách {cus.Code} đang giữ {holding:0.#} {type} – không ghi thất thoát {qty:0.#} được");
                    }
                    decimal fee = PackagingTypeCatalog.LossFee(db, Svc<IOptions<BusinessRulesOptions>>(sp).Value, type);
                    var pv = new AgentPreview
                    {
                        Summary = $"Ghi thất thoát {qty:0.#} {type} của {cus.Name}" + (fee > 0 ? $" – phí dự kiến tối đa {qty * fee:#,0} đ ({fee:#,0} đ / cái, phần vượt ngưỡng)" : " – chưa đặt phí thất thoát cho loại này (Đóng gói & Giao hàng › Loại hộp / khay)"),
                        Counterparty = $"{cus.Code} {cus.Name}", MainValue = $"{qty:0.#}", DocumentNo = dlvNo, OpenLink = "#/log-packaging-ledger",
                        LineHeaders = ["Loại", "Đang giữ", "Thất thoát", "Tỷ lệ thất thoát kỳ", "Phí / cái"],
                        Lines = [[type, $"{holding:0.#}", $"{qty:0.#}", item?.LossRatePct is { } lr ? $"{lr:0.#}%" : "–", fee > 0 ? $"{fee:#,0} đ" : "chưa cấu hình"]],
                        Payload = new JsonObject { ["customerId"] = cus.Id.ToString(), ["type"] = type, ["qty"] = qty, ["deliveryId"] = dlvId?.ToString(), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                    if (fee <= 0)
                    {
                        pv.Warnings.Add("Loại hộp này chưa đặt phí thất thoát (Loại hộp / khay) – bảng kê chưa tính tiền");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IPackagingLedgerService>(sp).Record(new PackagingRecordRequest
                    {
                        CustomerId = G(p, "customerId"), TransactionType = "LOSS", PackagingType = S(p, "type"), Quantity = p["qty"]!.GetValue<decimal>(),
                        DeliveryOrderId = p["deliveryId"]?.GetValue<string?>() is { } d ? Guid.Parse(d) : null, Reason = $"[Agent] {S(p, "reason")}",
                    });
                    return r.ResponseCode == 200 ? (null, "Đã ghi thất thoát hộp / khay" + (r.ResponseMessage is { Length: > 0 } m && m != "OK" ? $" – {m}" : string.Empty)) : (r.ResponseMessage ?? "Không ghi được thất thoát", null);
                },
            },
        ];
    }
}
