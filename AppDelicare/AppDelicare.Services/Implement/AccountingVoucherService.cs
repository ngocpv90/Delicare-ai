using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-2 (D-131 d, e – anh chốt 2026-11-26): Kế toán › Chứng từ kế toán. Chứng từ gửi MISA AMIS <b>tính khi đọc</b> từ nghiệp vụ đã xong (không bảng mới),
    /// số chứng từ Delicare = số nghiệp vụ gốc: bán hàng tháng × khách (bảng kê khách đã xác nhận – ACC-3), nhập mua (phiếu nhận đã ghi sổ: số đạt × giá),
    /// xuất NVL theo phiếu xuất (ca → buổi → mã thành phẩm = đối tượng tập hợp chi phí), nhập kho / xuất thành phẩm khác theo ngày × buổi, kho khác
    /// (điều chỉnh, kiểm kê, hủy lô, trả hàng), bảng công tháng. Trạng thái gửi lưu bằng <c>AuditChange</c> (EntityName <c>AccountingVoucher</c>,
    /// IdChange = Guid từ khóa chứng từ; Changes = JSON khóa · kỳ · băm nội dung · số tiền): đã tải Excel / gửi = Chờ lập trên AMIS; kế toán đánh dấu = Đã lập;
    /// nội dung đổi hoặc nghiệp vụ gốc hủy sau khi gửi = Đã sửa – cần điều chỉnh. Thiếu mã trên AMIS (bản chụp ACC-1) = Lỗi.
    /// </summary>
    public partial class AccountingVoucherService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<MisaOptions> misa,
        IAmisCatalogService amis, IBillingStatementService billing) : IAccountingVoucherService
    {
        public const string LogEntity = "AccountingVoucher";
        public const string ActionExported = "Exported";
        public const string ActionCreated = "MarkedCreated";
        /// <summary>ACC-3 ghi khi khách xác nhận bảng kê (EntityName BillingStatement, IdChange = khách, Changes chứa "yyyy-MM").</summary>
        public const string BillingConfirmedAction = "CustomerConfirmed";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly MisaOptions _misa = misa.Value;
        private readonly IAmisCatalogService _amis = amis;
        private readonly IBillingStatementService _billing = billing;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed record LogEntry(string Key, string Month, string Hash, decimal? Amount, string? Number, string? Type, string Action, DateTime At, DateTime? DocDate, string? ObjectCode, string? ObjectName);

        private static Guid KeyGuid(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes(key)));

        private static (DateTime From, DateTime To, string Month) Period(string? month, DateTime today)
        {
            DateTime first = DateTime.TryParseExact(month ?? string.Empty, "yyyy-MM", Inv, DateTimeStyles.None, out var m) ? m : new DateTime(today.Year, today.Month, 1);
            return (first, first.AddMonths(1).AddDays(-1), first.ToString("yyyy-MM", Inv));
        }

        private static string Hash(VoucherModel v)
        {
            string s = JsonSerializer.Serialize(new { v.ObjectCode, v.Amount, v.Quantity, L = v.ListLine.Select(l => new { l.Code, l.Quantity, l.UnitPrice, l.Amount, l.Target }) });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
        }

        /// <summary>Ca → buổi theo mã ca (SANG_6H, TRUA, TOI, DEM_23H…), thiếu thì theo tên ca.</summary>
        public static string? MealOfShift(string? code, string? name)
        {
            foreach (string s in new[] { code, MenuService.NormName(name).ToUpperInvariant().Replace(' ', '_') })
            {
                string c = (s ?? string.Empty).ToUpperInvariant();
                if (c.StartsWith("SANG", StringComparison.Ordinal)) return "SANG";
                if (c.StartsWith("TRUA", StringComparison.Ordinal)) return "TRUA";
                if (c.StartsWith("TOI", StringComparison.Ordinal)) return "TOI";
                if (c.StartsWith("DEM", StringComparison.Ordinal)) return "DEM";
            }
            return null;
        }

        private string? ProductCode(string? meal) => meal != null && _misa.ProductByMeal.TryGetValue(meal, out var c) ? c : null;

        private static string MealName(string? m) => (m ?? string.Empty).ToUpperInvariant() switch
        {
            "SANG" => "Sáng", "TRUA" => "Trưa", "TOI" => "Tối", "DEM" => "Đêm", _ => m ?? string.Empty,
        };

        // ============================================================ dựng chứng từ của kỳ
        private async Task<List<VoucherModel>> BuildAsync(DateTime from, DateTime to)
        {
            var db = _unitOfWork.DbContext;
            var list = new List<VoucherModel>();
            DateTime end = to.AddDays(1);
            var ingredients = await db.Ingredient.AsNoTracking().Select(i => new { i.Id, i.Code, i.Name, i.BaseUnitId }).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().Select(u => new { u.Id, u.Name, u.Code }).ToDictionaryAsync(u => u.Id);
            string UnitOf(Guid id) => units.TryGetValue(id, out var u) ? u.Name ?? u.Code ?? string.Empty : string.Empty;

            // (a) Bán hàng tháng × khách (bảng kê).
            var customers = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= from && o.MealDate < end && o.Status == OrderStatus.Reconciled)
                .Select(o => o.CustomerId).Distinct().ToListAsync();
            // ACC-3 (D-131 f): khách đã xác nhận bảng kê tháng (trạng thái mới nhất Đã xác nhận / Đã lập phiếu bán).
            var confirmed = (await BillingConfirmation.StatesAsync(db, from.ToString("yyyy-MM", Inv)))
                .Where(kv => kv.Value.Code is BillingConfirmation.StateConfirmed or BillingConfirmation.StateInvoiced).Select(kv => kv.Key).ToHashSet();
            foreach (var customerId in customers)
            {
                var st = (await _billing.GetStatement(new BillingStatementRequest { CustomerId = customerId, From = from, To = to })).Data;
                if (st == null || (st.ListRow.Count == 0 && st.ListPending.Count == 0))
                {
                    continue;
                }
                var v = new VoucherModel
                {
                    Key = $"{VoucherType.Sale}|{customerId:N}|{from:yyyyMM}", Type = VoucherType.Sale, Number = $"BK-{st.CustomerCode}-{from:yyMM}", Date = to,
                    ObjectCode = st.CustomerCode, ObjectName = st.CustomerName, Source = "Bảng kê thanh toán tháng", Amount = st.SubTotal,
                    Quantity = st.TotalBilledQuantity,
                };
                foreach (var g in st.ListRow.GroupBy(r => (r.MealType ?? string.Empty).ToUpperInvariant()).OrderBy(g => g.Key))
                {
                    decimal qty = g.Sum(r => r.BilledQuantity), amount = g.Sum(r => r.Amount);
                    v.ListLine.Add(new VoucherLineModel
                    {
                        Code = ProductCode(g.Key), Name = "Suất ăn " + MealName(g.Key).ToLowerInvariant(), Unit = "Suất", Quantity = qty,
                        UnitPrice = qty == 0 ? null : Math.Round(amount / qty, 2), Amount = amount, VatPct = st.VatPct,
                    });
                }
                if (st.ContainerFeeAmount > 0)
                {
                    v.ListLine.Add(new VoucherLineModel { Code = _misa.ReasonCodes.GetValueOrDefault("CONTAINER_FEE"), Name = "Phí thất thoát khay hộp", Unit = "Lần", Quantity = 1, UnitPrice = st.ContainerFeeAmount, Amount = st.ContainerFeeAmount, VatPct = st.VatPct });
                }
                if (st.ListPending.Count > 0)
                {
                    (v.Status, v.Note, v.FixLink, v.FixLabel) = (VoucherStatus.Pending, $"Còn {st.ListPending.Count} đơn chờ đối soát", "#/crm-reconciliation", "Đối soát");
                }
                else if (!confirmed.Contains(customerId))
                {
                    (v.Status, v.Note, v.FixLink, v.FixLabel) = (VoucherStatus.Pending, "Bảng kê chưa được khách xác nhận", "#/crm-billing", "Bảng kê");
                }
                v.Note ??= string.Join(" · ", v.ListLine.Where(l => l.Code != null).Select(l => l.Code));
                list.Add(v);
            }

            // (b) Nhập mua: phiếu nhận đã ghi sổ.
            var receipts = await db.GoodsReceipt.AsNoTracking().Where(r => !r.IsDeleted && r.Status == "Posted" && r.ReceiptDate >= from && r.ReceiptDate < end).ToListAsync();
            var rIds = receipts.Select(r => r.Id).ToList();
            var rLines = (await db.GoodsReceiptLine.AsNoTracking().Where(l => !l.IsDeleted && rIds.Contains(l.GoodsReceiptId)).ToListAsync()).ToLookup(l => l.GoodsReceiptId);
            var supplierIds = receipts.Select(r => r.SupplierId).Distinct().ToList();
            var suppliers = await db.Supplier.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name });
            foreach (var r in receipts)
            {
                var lines = rLines[r.Id].ToList();
                var s = suppliers.GetValueOrDefault(r.SupplierId);
                var v = new VoucherModel
                {
                    Key = $"{VoucherType.Purchase}|{r.Id:N}", Type = VoucherType.Purchase, Number = r.ReceiptNumber ?? r.ReceiptNo ?? r.Id.ToString("N")[..8], Date = r.ReceiptDate,
                    ObjectCode = s?.Code, ObjectName = s?.Name, Source = "Phiếu nhận đã ghi sổ (số đạt)",
                    Note = $"Giao {N(lines.Sum(l => l.ReceivedQuantity))} · đạt {N(lines.Sum(l => l.AcceptedQuantity))} · loại {N(lines.Sum(l => l.RejectedQuantity))}",
                };
                foreach (var l in lines.Where(l => l.AcceptedQuantity > 0))
                {
                    var ing = ingredients.GetValueOrDefault(l.IngredientId);
                    v.ListLine.Add(new VoucherLineModel
                    {
                        Code = ing?.Code, Name = ing?.Name, Unit = UnitOf(l.UnitId != Guid.Empty ? l.UnitId : l.UomId), Quantity = l.AcceptedQuantity, UnitPrice = l.UnitCost,
                        Amount = l.UnitCost is { } p ? Math.Round(p * l.AcceptedQuantity, 0) : null, VatPct = null,
                    });
                }
                v.Amount = v.ListLine.Sum(x => x.Amount ?? 0m);
                if (v.ListLine.Any(x => x.UnitPrice == null))
                {
                    (v.Status, v.Note, v.FixLink, v.FixLabel) = (VoucherStatus.Error, "Thiếu đơn giá trên phiếu nhận", "#/scm-gr", "Phiếu nhận");
                }
                list.Add(v);
            }

            // (c) Xuất NVL theo phiếu xuất (sổ kho ISSUE / REVERSAL của phiếu) – đối tượng THCP = thành phẩm của buổi (ca của lệnh SX).
            var issues = await db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.IssueDate >= from && i.IssueDate < end && (i.Status == "Posted" || i.Status == "Issued")).ToListAsync();
            var iIds = issues.Select(i => i.Id).ToList();
            var ledger = (await db.InventoryTransaction.AsNoTracking().Where(t => !t.IsDeleted && t.ReferenceType == "MaterialIssue" && t.ReferenceId != null && iIds.Contains(t.ReferenceId.Value)).ToListAsync())
                .ToLookup(t => t.ReferenceId!.Value);
            var poIds = issues.Select(i => i.ProductionOrderId).Distinct().ToList();
            var shiftOfPo = await db.ProductionOrder.AsNoTracking().Where(p => poIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ShiftId);
            var shifts = await db.Shift.AsNoTracking().Select(s => new { s.Id, s.Code, s.Name }).ToDictionaryAsync(s => s.Id);
            foreach (var i in issues)
            {
                var rows = ledger[i.Id].ToList();
                if (rows.Count == 0)
                {
                    continue;
                }
                Guid? shiftId = shiftOfPo.GetValueOrDefault(i.ProductionOrderId);
                var sh = shiftId is { } sid ? shifts.GetValueOrDefault(sid) : null;
                string? meal = MealOfShift(sh?.Code, sh?.Name);
                string? target = ProductCode(meal);
                var v = new VoucherModel
                {
                    Key = $"{VoucherType.Issue}|{i.Id:N}", Type = VoucherType.Issue, Number = i.IssueNumber ?? i.IssueNo ?? i.Id.ToString("N")[..8], Date = i.IssueDate,
                    ObjectCode = target, ObjectName = meal != null ? "Buổi " + MealName(meal) : "Không gắn ca", Source = "Phiếu xuất theo ca đã ghi sổ",
                };
                foreach (var g in rows.GroupBy(t => t.IngredientId))
                {
                    decimal qty = g.Sum(t => t.QuantityOut - t.QuantityIn);
                    if (qty == 0)
                    {
                        continue;
                    }
                    decimal amount = g.Sum(t => (t.QuantityOut - t.QuantityIn) * (t.UnitCost ?? 0m));
                    var ing = ingredients.GetValueOrDefault(g.Key);
                    v.ListLine.Add(new VoucherLineModel
                    {
                        Code = ing?.Code, Name = ing?.Name, Unit = UnitOf(g.First().UnitId != Guid.Empty ? g.First().UnitId : g.First().UomId), Quantity = qty,
                        UnitPrice = Math.Round(amount / qty, 2), Amount = Math.Round(amount, 0), Target = target,
                    });
                }
                if (v.ListLine.Count == 0)
                {
                    continue; // phiếu đã đảo hết – không còn chứng từ (đã gửi → dòng "cần điều chỉnh" do lịch sử gửi)
                }
                v.Amount = v.ListLine.Sum(x => x.Amount ?? 0m);
                v.Note = $"{v.ListLine.Count} dòng";
                if (target == null)
                {
                    (v.Status, v.Note) = (VoucherStatus.Error, "Phiếu xuất không gắn ca / buổi – chưa có đối tượng tập hợp chi phí (GAP-INV-02)");
                }
                list.Add(v);
            }

            // (e) Thành phẩm theo ngày × buổi: nhập kho = suất đã giao (gồm suất bù) + suất hủy / dư; xuất khác = suất bù (đơn bù giá 0) + hủy / dư.
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= from && o.MealDate < end
                    && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Reconciled))
                .Select(o => new { o.Id, o.MealDate, o.Note, o.TotalAmount }).ToListAsync();
            var oIds = orders.Select(o => o.Id).ToList();
            var oLines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && oIds.Contains(l.OrderId)).Select(l => new { l.OrderId, l.MealType, l.Quantity }).ToListAsync();
            var comp = orders.Where(o => (o.Note ?? string.Empty).Contains("[DON_BU:", StringComparison.Ordinal) && (o.TotalAmount ?? 0m) == 0m).Select(o => o.Id).ToHashSet();
            var orderDate = orders.ToDictionary(o => o.Id, o => o.MealDate.Date);
            var waste = (await db.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.OccurredAt >= from && w.OccurredAt < end && w.QuantityMeals != null && w.QuantityMeals > 0)
                .Select(w => new { w.OccurredAt, w.ShiftId, w.QuantityMeals, w.WasteType }).ToListAsync())
                .Select(w => { var sh = w.ShiftId is { } sid ? shifts.GetValueOrDefault(sid) : null; return new { Date = w.OccurredAt.Date, Meal = MealOfShift(sh?.Code, sh?.Name) ?? "TRUA", Qty = w.QuantityMeals!.Value, w.WasteType }; })
                .ToList();
            var days = oLines.Select(l => (Date: orderDate[l.OrderId], Meal: (l.MealType ?? string.Empty).ToUpperInvariant())).Concat(waste.Select(w => (w.Date, w.Meal))).Distinct()
                .OrderBy(x => x.Date).ThenBy(x => MealRank(x.Meal)).ToList();
            foreach (var (date, meal) in days)
            {
                decimal delivered = oLines.Where(l => orderDate[l.OrderId] == date && string.Equals(l.MealType, meal, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Quantity);
                decimal compensation = oLines.Where(l => comp.Contains(l.OrderId) && orderDate[l.OrderId] == date && string.Equals(l.MealType, meal, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Quantity);
                decimal wasted = waste.Where(w => w.Date == date && w.Meal == meal).Sum(w => w.Qty);
                string? code = ProductCode(meal);
                if (delivered + wasted > 0)
                {
                    list.Add(new VoucherModel
                    {
                        Key = $"{VoucherType.FinishedIn}|{date:yyyyMMdd}|{meal}", Type = VoucherType.FinishedIn, Number = $"NTP-{date:yyMMdd}-{meal}", Date = date,
                        ObjectCode = code, ObjectName = "Suất ăn " + MealName(meal).ToLowerInvariant(), Source = "Suất đạt sau QC (đã giao + hủy / dư)", Quantity = delivered + wasted,
                        Note = "AMIS tính giá thành cuối kỳ",
                        ListLine = [new VoucherLineModel { Code = code, Name = "Suất ăn " + MealName(meal).ToLowerInvariant(), Unit = "Suất", Quantity = delivered + wasted }],
                    });
                }
                if (compensation + wasted > 0)
                {
                    var v = new VoucherModel
                    {
                        Key = $"{VoucherType.FinishedOut}|{date:yyyyMMdd}|{meal}", Type = VoucherType.FinishedOut, Number = $"XTP-{date:yyMMdd}-{meal}", Date = date,
                        ObjectCode = code, ObjectName = "Suất ăn " + MealName(meal).ToLowerInvariant(), Source = "Suất bù · suất hủy / dư", Quantity = compensation + wasted,
                        Note = $"Suất bù {N(compensation)} · suất hủy / dư {N(wasted)}",
                    };
                    if (compensation > 0)
                    {
                        v.ListLine.Add(new VoucherLineModel { Code = code, Name = "Suất bù", Unit = "Suất", Quantity = compensation, Target = _misa.ReasonCodes.GetValueOrDefault("COMPENSATION") ?? "Suất bù" });
                    }
                    if (wasted > 0)
                    {
                        v.ListLine.Add(new VoucherLineModel { Code = code, Name = "Suất hủy / dư", Unit = "Suất", Quantity = wasted, Target = _misa.ReasonCodes.GetValueOrDefault("MEAL_WASTE") ?? "Hủy / dư" });
                    }
                    list.Add(v);
                }
            }

            // (f) Kho khác: điều chỉnh / kiểm kê đã duyệt, hủy lô, trả hàng NCC – theo chứng từ gốc của sổ kho.
            var other = await db.InventoryTransaction.AsNoTracking().Where(t => !t.IsDeleted && t.TransactionDate >= from && t.TransactionDate < end
                    && (t.TransactionType == "ADJUSTMENT" || t.TransactionType == "WASTE" || t.TransactionType == "RETURN")).ToListAsync();
            foreach (var g in other.GroupBy(t => (t.TransactionType, Ref: t.ReferenceId?.ToString("N") ?? t.TransactionNumber ?? t.TransactionNo ?? t.Id.ToString("N"))))
            {
                var first = g.OrderBy(t => t.TransactionDate).First();
                string reason = _misa.ReasonCodes.GetValueOrDefault(g.Key.TransactionType ?? string.Empty) ?? g.Key.TransactionType switch
                {
                    "WASTE" => "Hủy lô", "RETURN" => "Trả lại hàng mua", _ => first.ReferenceType == "StockCount" ? "Kiểm kê" : "Điều chỉnh tồn",
                };
                var v = new VoucherModel
                {
                    Key = $"{VoucherType.StockOther}|{g.Key.TransactionType}|{g.Key.Ref}", Type = VoucherType.StockOther, Number = first.TransactionNumber ?? first.TransactionNo ?? g.Key.Ref[..8],
                    Date = first.TransactionDate.Date, ObjectCode = _misa.ReasonCodes.GetValueOrDefault(g.Key.TransactionType ?? string.Empty), ObjectName = reason, Source = first.ReferenceType switch
                    {
                        "StockCount" => "Phiếu kiểm kê đã duyệt", "ManualAdjustment" => "Điều chỉnh tồn đã duyệt", "LotExpiry" => "Lô hết hạn / hủy", _ => first.ReferenceType ?? reason,
                    },
                };
                foreach (var ig in g.GroupBy(t => t.IngredientId))
                {
                    decimal qty = ig.Sum(t => t.QuantityIn - t.QuantityOut);
                    if (qty == 0)
                    {
                        continue;
                    }
                    var ing = ingredients.GetValueOrDefault(ig.Key);
                    decimal amount = ig.Sum(t => (t.QuantityIn - t.QuantityOut) * (t.UnitCost ?? 0m));
                    v.ListLine.Add(new VoucherLineModel
                    {
                        Code = ing?.Code, Name = ing?.Name, Unit = UnitOf(ig.First().UnitId != Guid.Empty ? ig.First().UnitId : ig.First().UomId), Quantity = qty,
                        UnitPrice = Math.Round(amount / qty, 2), Amount = Math.Round(amount, 0), Target = reason, Note = first.Reason,
                    });
                }
                if (v.ListLine.Count == 0)
                {
                    continue;
                }
                v.Amount = v.ListLine.Sum(x => x.Amount ?? 0m);
                v.Note = $"{v.ListLine.Count} dòng · {(v.Amount >= 0 ? "nhập" : "xuất")} {N(Math.Abs(v.Amount ?? 0m))}";
                list.Add(v);
            }

            // (g) Bảng công tháng – chỉ Excel (Chấm công › Bảng công tháng).
            if (await db.AttendanceEvent.AsNoTracking().AnyAsync(a => !a.IsDeleted && a.EventAt >= from && a.EventAt < end))
            {
                list.Add(new VoucherModel
                {
                    Key = $"{VoucherType.Timesheet}|{from:yyyyMM}", Type = VoucherType.Timesheet, Number = $"BC-{from:yyMM}", Date = to, ObjectName = "Bảng công tháng",
                    Source = "Chấm công", Note = "Tải ở Nhân sự › Chấm công › Bảng công tháng (cho tính lương trên AMIS)", FixLink = "#/hr-attendance", FixLabel = "Bảng công",
                    Status = VoucherStatus.Ready,
                });
            }
            return list;
        }

        private static int MealRank(string m) => m switch { "SANG" => 1, "TRUA" => 2, "TOI" => 3, "DEM" => 4, _ => 9 };
        private static string N(decimal v) => v.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN"));

        /// <summary>Kiểm mã AMIS (bản chụp ACC-1): đối tượng (khách / NCC) + mã hàng / thành phẩm phải có trên AMIS.</summary>
        private static void CheckCodes(VoucherModel v, AmisCatalogSnapshot? snap)
        {
            if (v.Status is VoucherStatus.Error or VoucherStatus.Pending || v.Type == VoucherType.Timesheet)
            {
                return;
            }
            if (snap == null)
            {
                (v.Status, v.Note, v.FixLink, v.FixLabel) = (VoucherStatus.Error, "Chưa nhận danh mục AMIS – nhập ở Khớp mã AMIS", "#/acc-amis-code", "Khớp mã");
                return;
            }
            HashSet<string> Set(string kind) => snap.Items.TryGetValue(kind, out var l) ? l.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase) : [];
            var material = Set(AmisKind.Material);
            var missing = new List<string>();
            if (v.Type == VoucherType.Sale && v.ObjectCode != null && !Set(AmisKind.Customer).Contains(v.ObjectCode))
            {
                missing.Add($"khách {v.ObjectCode}");
            }
            if (v.Type == VoucherType.Purchase && v.ObjectCode != null && !Set(AmisKind.Supplier).Contains(v.ObjectCode))
            {
                missing.Add($"NCC {v.ObjectCode}");
            }
            if (v.Type is VoucherType.Issue && v.ObjectCode != null && !material.Contains(v.ObjectCode))
            {
                missing.Add($"thành phẩm {v.ObjectCode}");
            }
            missing.AddRange(v.ListLine.Where(l => !string.IsNullOrWhiteSpace(l.Code) && !material.Contains(l.Code!) && !(v.Type == VoucherType.Sale && l.Unit == "Lần"))
                .Select(l => l.Code!).Distinct().Take(5).Select(c => $"{c}"));
            if (v.ListLine.Any(l => string.IsNullOrWhiteSpace(l.Code) && !(v.Type == VoucherType.Sale && l.Unit == "Lần")))
            {
                missing.Add("dòng thiếu mã (khai Integration:Misa:ProductByMeal)");
            }
            if (missing.Count > 0)
            {
                (v.Status, v.FixLink, v.FixLabel) = (VoucherStatus.Error, "#/acc-amis-code", "Ghép mã");
                v.Note = "Chưa có trên AMIS: " + string.Join(", ", missing.Take(5)) + (missing.Count > 5 ? "…" : string.Empty);
            }
        }

        private async Task<List<LogEntry>> LogAsync(string month)
        {
            DateTime monthStart = DateTime.ParseExact(month, "yyyy-MM", Inv);
            var rows = await _unitOfWork.DbContext.AuditChange.AsNoTracking()
                .Where(a => !a.IsDeleted && a.EntityName == LogEntity && a.Changes != null && a.CreatedDate >= monthStart) // lọc kỳ ở bộ nhớ – LIKE trên Changes quét chậm
                .OrderBy(a => a.CreatedDate).Select(a => new { a.Action, a.Changes, a.CreatedDate }).ToListAsync();
            var outList = new List<LogEntry>();
            foreach (var r in rows)
            {
                try
                {
                    var j = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.Changes!)!;
                    string S(string k) => j.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : string.Empty;
                    decimal? D(string k) => j.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDecimal() : null;
                    DateTime? T(string k) => DateTime.TryParse(S(k), Inv, DateTimeStyles.None, out var t) ? t : null;
                    if (S("m") != month) continue;
                    outList.Add(new LogEntry(S("k"), S("m"), S("h"), D("a"), S("n"), S("t"), r.Action ?? string.Empty, r.CreatedDate, T("d"), S("oc"), S("on")));
                }
                catch (JsonException)
                {
                    // bản ghi lạ – bỏ qua
                }
            }
            return outList;
        }

        /// <summary>Chứng từ của kỳ + trạng thái (lỗi mã, lịch sử gửi, đã sửa sau khi gửi).</summary>
        public async Task<(List<VoucherModel> List, bool HasCatalog, DateTime From, DateTime To, string Month)> ComputeAsync(string? month)
        {
            var (from, to, m) = Period(month, _clock.Today);
            var list = await BuildAsync(from, to);
            var snap = await _amis.GetSnapshotAsync();
            var log = (await LogAsync(m)).GroupBy(l => l.Key).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var v in list)
            {
                v.TypeName = VoucherType.Label(v.Type);
                v.Tab = VoucherType.Tab(v.Type);
                v.Hash = Hash(v);
                CheckCodes(v, snap);
                if (log.TryGetValue(v.Key, out var hist) && hist.Where(h => h.Action == ActionExported).OrderBy(h => h.At).LastOrDefault() is { } sent)
                {
                    v.SentAt = sent.At;
                    if (sent.Hash != v.Hash)
                    {
                        v.Status = VoucherStatus.Changed;
                        v.Note = $"Nghiệp vụ gốc đổi sau khi gửi (đã gửi {N(sent.Amount ?? 0m)}) – gửi lại hoặc lập điều chỉnh trên AMIS";
                    }
                    else if (v.Status != VoucherStatus.Error)
                    {
                        v.Status = hist.Any(h => h.Action == ActionCreated && h.Hash == v.Hash && h.At >= sent.At) ? VoucherStatus.Created : VoucherStatus.Waiting;
                    }
                }
                if (string.IsNullOrEmpty(v.Status))
                {
                    v.Status = VoucherStatus.Ready;
                }
                v.StatusName = VoucherStatus.Label(v.Status);
            }
            // Đã gửi nhưng nghiệp vụ gốc không còn (hủy / đảo) → dòng điều chỉnh.
            var present = list.Select(v => v.Key).ToHashSet();
            foreach (var (key, hist) in log.Where(kv => !present.Contains(kv.Key)))
            {
                var sent = hist.Where(h => h.Action == ActionExported).OrderBy(h => h.At).LastOrDefault();
                if (sent == null || hist.Any(h => h.Action == "Adjusted" && h.At > sent.At))
                {
                    continue;
                }
                list.Add(new VoucherModel
                {
                    Key = key, Type = sent.Type ?? string.Empty, TypeName = VoucherType.Label(sent.Type), Tab = VoucherType.Tab(sent.Type ?? string.Empty), Number = sent.Number ?? key,
                    Date = sent.DocDate ?? to, ObjectCode = sent.ObjectCode, ObjectName = sent.ObjectName, Source = "Nghiệp vụ gốc đã hủy sau khi gửi", Amount = -(sent.Amount ?? 0m),
                    Status = VoucherStatus.Changed, StatusName = VoucherStatus.Label(VoucherStatus.Changed), SentAt = sent.At,
                    Note = "Nghiệp vụ gốc đã hủy / đảo sau khi gửi AMIS – lập chứng từ điều chỉnh trên AMIS (tự lập ĐC-: ACC-4)",
                });
            }
            return (list.OrderBy(v => v.Date).ThenBy(v => v.Type).ThenBy(v => v.Number, StringComparer.Ordinal).ToList(), snap != null, from, to, m);
        }

        public async Task<IResult<VoucherMonthModel>> GetMonth(string? month, string? tab)
        {
            try
            {
                var (list, hasCatalog, from, to, m) = await ComputeAsync(month);
                static bool Need(VoucherModel v) => v.Status is VoucherStatus.Error or VoucherStatus.Changed;
                var model = new VoucherMonthModel
                {
                    Month = m, From = from, To = to, HasAmisCatalog = hasCatalog, Total = list.Count,
                    SaleAmount = list.Where(v => v.Type == VoucherType.Sale && v.Status != VoucherStatus.Changed).Sum(v => v.Amount ?? 0m),
                    SaleCount = list.Count(v => v.Type == VoucherType.Sale),
                    IssueAmount = list.Where(v => v.Type == VoucherType.Issue && v.Status != VoucherStatus.Changed).Sum(v => v.Amount ?? 0m),
                    IssueCount = list.Count(v => v.Type == VoucherType.Issue),
                    CreatedCount = list.Count(v => v.Status == VoucherStatus.Created),
                    NeedsAction = list.Count(Need), ErrorCount = list.Count(v => v.Status == VoucherStatus.Error), ChangedCount = list.Count(v => v.Status == VoucherStatus.Changed),
                    ReadyCount = list.Count(v => v.Status == VoucherStatus.Ready),
                };
                model.TabCount["need"] = model.NeedsAction;
                foreach (string t in new[] { "sale", "purchase", "stock", "timesheet" })
                {
                    model.TabCount[t] = list.Count(v => v.Tab == t);
                }
                model.TabCount["all"] = list.Count;
                string tb = string.IsNullOrWhiteSpace(tab) ? "need" : tab.Trim().ToLowerInvariant();
                model.ListVoucher = tb switch { "need" => [.. list.Where(Need)], "all" => list, _ => [.. list.Where(v => v.Tab == tb)] };
                return Result<VoucherMonthModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonth: ", ex.ToString()));
                throw;
            }
        }

        private async Task WriteLogAsync(IEnumerable<VoucherModel> vouchers, string month, string action)
        {
            if (_currentActor.Actor is not { } actor)
            {
                return;
            }
            var db = _unitOfWork.DbContext;
            foreach (var v in vouchers)
            {
                db.AuditChange.Add(new AuditChange
                {
                    Id = Guid.NewGuid(), IdChange = KeyGuid(v.Key), EntityName = LogEntity, Action = action, CreatedDate = _clock.Now, CreatedBy = actor.UserId, IsDeleted = false,
                    Changes = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["k"] = v.Key, ["m"] = month, ["h"] = v.Hash, ["a"] = v.Amount, ["n"] = v.Number, ["t"] = v.Type,
                        ["d"] = v.Date.ToString("yyyy-MM-dd", Inv), ["oc"] = v.ObjectCode, ["on"] = v.ObjectName,
                    }),
                });
            }
            await db.SaveChangesAsync();
        }

        /// <summary>Kế toán đánh dấu đã lập trên AMIS (luồng Excel – chưa có API ACC-7) cho chứng từ đang "Chờ lập trên AMIS".</summary>
        public async Task<IResult<int>> MarkCreated(VoucherActionRequest request)
        {
            try
            {
                var (list, _, _, _, m) = await ComputeAsync(request.Month);
                var keys = request.ListKey.ToHashSet();
                var pick = list.Where(v => keys.Contains(v.Key) && v.Status == VoucherStatus.Waiting).ToList();
                if (pick.Count == 0)
                {
                    return Result<int>.Fail(ConstantHelper.Message.VoucherNothingToMark);
                }
                await WriteLogAsync(pick, m, ActionCreated);
                return Result<int>.Success(pick.Count, string.Format(ConstantHelper.Message.VoucherMarkedCreated, pick.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " MarkCreated: ", ex.ToString()));
                throw;
            }
        }
    }
}
