using AppDelicare.Data.Repositories;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>UI-6.51 (D-137 h): màn "Kế hoạch mua" – ma trận nguyên liệu × ngày cần hàng + trang chi tiết 5 bước của một ô.</summary>
    public sealed partial class PurchasePlanService
    {
        private sealed record PoLine(Guid PoId, string? Number, Guid SupplierId, string? Status, string? Note, DateTime Date, Guid IngredientId, decimal Quantity, Guid UnitId, decimal Amount, Guid? PrId);

        private sealed record PrLine(Guid PrId, string? Number, string? Status, string? Note, DateTime Date, Guid IngredientId, decimal Quantity, Guid UnitId);

        private static readonly Dictionary<string, string> StatusNames = new()
        {
            ["ORDERED"] = "Đã đặt", ["APPROVED"] = "Đã duyệt – chờ gửi NCC", ["PENDING"] = "Chờ duyệt", ["DRAFT"] = "Nháp – chờ gửi duyệt", ["NEED_PICK"] = "Chờ chọn NCC",
            ["DUE_TODAY"] = "Đặt hôm nay", ["LATE"] = "Quá hạn đặt", ["PLANNED"] = "Dự kiến", ["ENOUGH"] = "Đủ hàng", ["NONE"] = string.Empty,
        };

        public async Task<IResult<PurchasePlanBoardModel>> GetBoard(PurchasePlanBoardRequest request)
        {
            try
            {
                int days = request.Days ?? 7;
                if (days is < 1 or > 31)
                {
                    return Result<PurchasePlanBoardModel>.Fail(ConstantHelper.Message.MrpHorizonDaysRange);
                }
                var board = await BuildBoardAsync(request, true);
                return board == null ? Result<PurchasePlanBoardModel>.Fail("Không tính được kế hoạch mua") : Result<PurchasePlanBoardModel>.Success(board, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        private async Task<(List<PoLine> Pos, List<PrLine> Prs)> OrdersAsync(DateTime start, DateTime finish)
        {
            var db = unitOfWork.DbContext;
            DateTime begin = start.Date, end = finish.Date.AddDays(1);
            var pos = await (from l in db.PurchaseOrderLine.AsNoTracking()
                             join o in db.PurchaseOrder.AsNoTracking() on l.PurchaseOrderId equals o.Id
                             where !l.IsDeleted && !o.IsDeleted && o.Status != PurchaseOrderStatus.Cancelled && (o.ExpectedDeliveryDate ?? o.ExpectedDate) >= begin && (o.ExpectedDeliveryDate ?? o.ExpectedDate) < end
                             select new PoLine(o.Id, o.PurchaseOrderNumber ?? o.PONo, o.SupplierId, o.Status, o.Note, (o.ExpectedDeliveryDate ?? o.ExpectedDate)!.Value, l.IngredientId, l.Quantity,
                                 l.UnitId != Guid.Empty ? l.UnitId : l.UomId, l.LineAmount, o.PurchaseRequestId)).ToListAsync();
            var open = new[] { PurchaseRequestStatus.Draft, PurchaseRequestStatus.PendingApproval, PurchaseRequestStatus.Approved };
            var prs = await (from l in db.PurchaseRequestLine.AsNoTracking()
                             join p in db.PurchaseRequest.AsNoTracking() on l.PurchaseRequestId equals p.Id
                             where !l.IsDeleted && !p.IsDeleted && p.Status != null && open.Contains(p.Status) && l.Quantity > 0
                                && (l.RequiredDate ?? p.RequiredDate) >= begin && (l.RequiredDate ?? p.RequiredDate) < end
                             select new PrLine(p.Id, p.RequestNumber ?? p.RequestNo, p.Status, p.Note, (l.RequiredDate ?? p.RequiredDate)!.Value, l.IngredientId, l.Quantity,
                                 l.UnitId != Guid.Empty ? l.UnitId : l.UomId)).ToListAsync();
            // Đề nghị Đã duyệt đã lên PO cho nguyên liệu đó → tính ở PO.
            var covered = pos.Where(p => p.PrId != null).Select(p => (p.PrId!.Value, p.IngredientId)).ToHashSet();
            return (pos, [.. prs.Where(p => !covered.Contains((p.PrId, p.IngredientId)))]);
        }

        private async Task<PurchasePlanBoardModel?> BuildBoardAsync(PurchasePlanBoardRequest request, bool withKpi)
        {
            var db = unitOfWork.DbContext;
            DateTime today = clock.Today, from = (request.From ?? today).Date, to = from.AddDays((request.Days ?? 7) - 1);
            DateTime hzFrom = from > today ? from : today.AddDays(1);
            MaterialHorizonModel? hz = null;
            var board = new PurchasePlanBoardModel { Today = today, FromDate = from, ToDate = to, RunTimes = [_plan.RunTime(0), _plan.RunTime(1)], SendTime = _plan.SendTime };
            if (to >= hzFrom)
            {
                var (model, error) = await materialRequirementService.PlanHorizonAsync(new MaterialHorizonRequest { FromDate = hzFrom, Days = (to - hzFrom).Days + 1, OrganizationUnitId = request.OrganizationUnitId });
                if (model == null)
                {
                    board.ListWarning.Add(error ?? "Không tính được kế hoạch mua");
                }
                hz = model;
                board.ListWarning.AddRange(model?.ListWarning ?? []);
            }
            for (DateTime d = from; d <= to; d = d.AddDays(1))
            {
                var hd = hz?.ListDay.FirstOrDefault(x => x.Date.Date == d);
                board.ListDay.Add(new PurchasePlanDayModel { Date = d, IsToday = d == today, Source = hd?.Source, SourceName = hd?.SourceName });
            }
            var (pos, prs) = await OrdersAsync(from, to);
            var ingIds = (hz?.ListItem.Select(i => i.IngredientId) ?? []).Concat(pos.Select(p => p.IngredientId)).Concat(prs.Select(p => p.IngredientId)).Distinct().ToList();
            var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var catIds = ings.Values.Select(i => i.IngredientCategoryId ?? i.CategoryId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var cats = await db.IngredientCategory.AsNoTracking().Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name ?? c.Code);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Symbol ?? u.Name ?? u.Code);
            var supIds = pos.Select(p => p.SupplierId).Distinct().ToList();
            var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name ?? s.Code);
            var conv = new Dictionary<(Guid, Guid), decimal?>();
            async Task<decimal> ToBase(decimal q, Guid fromUnit, Guid baseUnit, DateTime on)
            {
                if (fromUnit == baseUnit || baseUnit == Guid.Empty || fromUnit == Guid.Empty)
                {
                    return q;
                }
                if (!conv.TryGetValue((fromUnit, baseUnit), out var f))
                {
                    f = await uomConversionService.ConvertAsync(1m, fromUnit, baseUnit, on);
                    conv[(fromUnit, baseUnit)] = f;
                }
                return q * (f ?? 1m);
            }
            var subs = hz?.ListSubstitution ?? [];
            var stdOf = subs.GroupBy(s => s.SubIngredientId).ToDictionary(g => g.Key, g => g.First().StdIngredientId);
            var hasSub = subs.Select(s => s.StdIngredientId).ToHashSet();
            var rows = new List<PurchasePlanRowModel>();
            foreach (Guid id in ingIds)
            {
                var ing = ings.GetValueOrDefault(id);
                if (ing == null || ing.IsDeleted)
                {
                    continue; // nguyên liệu đã xóa (vd dữ liệu demo đã thu hồi) – không hiện trên kế hoạch
                }
                var item = hz?.ListItem.FirstOrDefault(i => i.IngredientId == id);
                Guid baseUnit = item?.UnitId ?? (ing?.BaseUnitId is { } bu && bu != Guid.Empty ? bu : ing?.BaseUomId ?? Guid.Empty);
                var row = new PurchasePlanRowModel
                {
                    IngredientId = id, IngredientCode = ing?.Code, IngredientName = ing?.Name, UnitName = item?.UnitName ?? units.GetValueOrDefault(baseUnit),
                    CategoryName = (ing?.IngredientCategoryId ?? ing?.CategoryId) is { } cid ? cats.GetValueOrDefault(cid) : null,
                    HasSubstitute = hasSub.Contains(id), IsSubstitute = stdOf.ContainsKey(id), SubstituteOfId = stdOf.TryGetValue(id, out var so) ? so : null,
                };
                if (row.HasSubstitute)
                {
                    var s = subs.First(x => x.StdIngredientId == id);
                    row.Note = $"Chuẩn · được thay bằng {s.SubIngredientName}";
                }
                else if (row.IsSubstitute)
                {
                    row.Note = $"dùng thay {subs.First(x => x.SubIngredientId == id).StdIngredientName} khi rẻ hơn ≥ {_plan.SubstituteMinSavingPct:0.#}%";
                }
                foreach (var day in board.ListDay)
                {
                    DateTime d = day.Date;
                    var cellHz = item?.ListDay.FirstOrDefault(c => c.Date.Date == d);
                    var myPos = pos.Where(p => p.IngredientId == id && p.Date.Date == d).ToList();
                    var myPrs = prs.Where(p => p.IngredientId == id && p.Date.Date == d).ToList();
                    var cell = new PurchasePlanCellModel { Date = d, Required = cellHz?.Required ?? 0m, OrderByDate = cellHz?.OrderByDate };
                    decimal qPo = 0m, qPr = 0m;
                    foreach (var p in myPos) qPo += await ToBase(p.Quantity, p.UnitId, baseUnit, d);
                    foreach (var p in myPrs) qPr += await ToBase(p.Quantity, p.UnitId, baseUnit, d);
                    decimal shortage = cellHz?.Shortage ?? 0m;
                    cell.Quantity = Math.Round(qPo + qPr + shortage, 2);
                    cell.ListPurchaseOrderId = [.. myPos.Select(p => p.PoId).Distinct()];
                    cell.PurchaseRequestId = myPrs.Select(p => (Guid?)p.PrId).FirstOrDefault() ?? myPos.Select(p => p.PrId).FirstOrDefault(x => x != null);
                    string bySupplier = string.Join(" · ", myPos.GroupBy(p => p.SupplierId).Select(g => $"{Short(sups.GetValueOrDefault(g.Key))} {Qty(g.Sum(x => x.Quantity))}"));
                    if (shortage > 0)
                    {
                        DateTime by = cellHz!.OrderByDate ?? d;
                        cell.Status = by < today ? "LATE" : by == today ? "DUE_TODAY" : "PLANNED";
                        cell.Sub = cell.Status == "LATE" ? $"hạn đặt {Day(by)}" + (cellHz.LeadTimeDays is > 0 ? $" – NCC cần {cellHz.LeadTimeDays} ngày" : string.Empty)
                            : cell.Status == "DUE_TODAY" ? item?.SupplierName ?? "chưa có NCC" + (cellHz.LeadTimeDays is > 0 ? $" · giao {cellHz.LeadTimeDays} ngày" : string.Empty)
                            : $"đặt {Day(by)}";
                    }
                    else if (myPrs.Any(p => p.Status == PurchaseRequestStatus.PendingApproval))
                    {
                        cell.Status = "PENDING";
                        string? why = myPrs.Select(p => p.Note).FirstOrDefault(n => n?.Contains("Cần duyệt:", StringComparison.Ordinal) == true);
                        cell.Sub = why == null ? "chờ duyệt" : Trim(why[(why.IndexOf("Cần duyệt:", StringComparison.Ordinal) + 10)..], 60) + " → Trưởng mua hàng";
                    }
                    else if (myPrs.Any(p => p.Status == PurchaseRequestStatus.Draft))
                    {
                        cell.Status = "DRAFT";
                        cell.Sub = myPrs.First(p => p.Status == PurchaseRequestStatus.Draft).Number;
                    }
                    else if (myPrs.Any(p => p.Status == PurchaseRequestStatus.Approved))
                    {
                        cell.Status = "NEED_PICK";
                        cell.Sub = "đề nghị đã duyệt – chọn NCC";
                    }
                    else if (myPos.Any(p => p.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.PendingApproval))
                    {
                        cell.Status = "PENDING";
                        cell.Sub = bySupplier + " · đơn mua chờ duyệt";
                    }
                    else if (myPos.Any(p => p.Status == PurchaseOrderStatus.Approved))
                    {
                        cell.Status = "APPROVED";
                        bool auto = myPos.Any(p => p.Note?.Contains(PurchasePlanRules.AutoApprovedNote, StringComparison.Ordinal) == true);
                        cell.StatusName = auto ? "Tự duyệt" : "Đã duyệt";
                        cell.Sub = $"{(auto ? "tự duyệt" : "đã duyệt")} · {bySupplier} · gửi {_plan.SendTime}";
                    }
                    else if (myPos.Count > 0)
                    {
                        cell.Status = "ORDERED";
                        cell.Sub = bySupplier;
                    }
                    else if (cell.Required > 0)
                    {
                        cell.Status = "ENOUGH";
                        cell.Quantity = Math.Round(cell.Required, 2);
                        cell.Sub = "đủ tồn / hàng về";
                    }
                    if (row.IsSubstitute && cell.Status is "NONE" or "ENOUGH" && subs.Any(s => s.SubIngredientId == id && s.Date.Date == d && !s.ListDish.Any(x => x.SubGross > 0)))
                    {
                        cell.Sub = "giữ " + subs.First(s => s.SubIngredientId == id).StdIngredientName;
                    }
                    cell.StatusName ??= StatusNames.GetValueOrDefault(cell.Status);
                    row.ListCell.Add(cell);
                }
                if (row.ListCell.Any(c => c.Status != "NONE") || row.HasSubstitute || row.IsSubstitute)
                {
                    rows.Add(row);
                }
            }
            // Theo nhóm nguyên liệu; dòng thay thế ngay dưới dòng chuẩn.
            var ordered = new List<PurchasePlanRowModel>();
            foreach (var r in rows.Where(r => !r.IsSubstitute).OrderBy(r => r.CategoryName ?? "~").ThenBy(r => r.IngredientName))
            {
                ordered.Add(r);
                ordered.AddRange(rows.Where(x => x.IsSubstitute && x.SubstituteOfId == r.IngredientId && !ordered.Contains(x)));
            }
            ordered.AddRange(rows.Where(r => !ordered.Contains(r)));
            board.ListRow = ordered;
            var dueIngs = ordered.Where(r => r.ListCell.Any(c => c.Status is "DUE_TODAY" or "LATE")).ToList();
            board.DueCount = dueIngs.Count;
            var last = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == "PurchasePlanRun" && !a.IsDeleted).OrderByDescending(a => a.CreatedDate).FirstOrDefaultAsync();
            if (last != null)
            {
                board.LastRunAt = last.CreatedDate;
                board.LastRunSummary = $"{last.Action}: " + (JsonConvert.DeserializeObject<Dictionary<string, object>>(last.Changes ?? "{}")?.GetValueOrDefault("Summary")?.ToString() ?? string.Empty);
            }
            if (withKpi)
            {
                await KpiAsync(board, pos);
            }
            return board;
        }

        private static string? Short(string? name) => name?.Replace("Công ty ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("TNHH ", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

        private static string Trim(string s, int max) => s.Length <= max ? s.Trim() : s[..max].Trim() + "…";

        private async Task KpiAsync(PurchasePlanBoardModel board, List<PoLine> pos)
        {
            var db = unitOfWork.DbContext;
            var k = board.Kpi;
            var due = board.ListRow.Where(r => r.ListCell.Any(c => c.Status == "DUE_TODAY")).ToList();
            var late = board.ListRow.Where(r => r.ListCell.Any(c => c.Status == "LATE")).ToList();
            k.DueToday = due.Count;
            k.Late = late.Count;
            k.LateNote = late.Count == 0 ? "không có" : string.Join(", ", late.Take(2).Select(r => r.IngredientName)) + " – cần NCC giao gấp";
            if (due.Count > 0)
            {
                var needs = due.SelectMany(r => r.ListCell.Where(c => c.Status == "DUE_TODAY").Select(c => new SupplierPick.Need(Guid.NewGuid(), r.IngredientId, c.Quantity,
                    board.ListRow.First(x => x.IngredientId == r.IngredientId).ListCell.Count > 0 ? Guid.Empty : Guid.Empty, c.Date))).ToList();
                var ingIds = due.Select(r => r.IngredientId).ToList();
                var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                needs = [.. needs.Select(n => n with { UnitId = ings.GetValueOrDefault(n.IngredientId) is { } i ? (i.BaseUnitId is { } b && b != Guid.Empty ? b : i.BaseUomId ?? Guid.Empty) : Guid.Empty })];
                var picks = await SupplierPick.SuggestAsync(db, uomConversionService, _rules, clock.Today, needs, _plan.SplitMaxCorrectionPct);
                k.DueTodayAmount = Math.Round(picks.Values.Sum(p => (p.OrderQuantity ?? p.Quantity) * (p.UnitPrice ?? 0m)), 0);
            }
            var pending = await db.PurchaseRequest.AsNoTracking().Where(p => !p.IsDeleted && p.Status == PurchaseRequestStatus.PendingApproval && p.Note != null && p.Note.StartsWith(PurchasePlanRules.Marker))
                .Select(p => p.Note).ToListAsync();
            k.PendingApproval = pending.Count;
            k.PendingNote = pending.Select(n => n!.Contains("Cần duyệt:", StringComparison.Ordinal) ? Trim(n[(n.IndexOf("Cần duyệt:", StringComparison.Ordinal) + 10)..], 50) : null).FirstOrDefault(x => x != null) ?? "không có";
            k.UnsentPurchaseOrder = await db.PurchaseOrder.AsNoTracking().CountAsync(o => !o.IsDeleted && o.Status == PurchaseOrderStatus.Approved);
            // Tiết kiệm nhờ thay thế trong tháng: quyết định đã lưu (mới nhất mỗi cặp × ngày) có ngày cần trong tháng.
            DateTime m0 = new(clock.Today.Year, clock.Today.Month, 1);
            var rowsSub = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == SubstitutionRules.EntityName && !a.IsDeleted && a.CreatedDate >= m0.AddDays(-10))
                .OrderByDescending(a => a.CreatedDate).Select(a => new { a.IdChange, a.Changes }).ToListAsync();
            var latest = rowsSub.GroupBy(r => r.IdChange).Select(g => JsonConvert.DeserializeObject<SubstitutionRules.Decision>(g.First().Changes ?? "{}"))
                .Where(d => d != null && d.Date >= m0 && d.Date < m0.AddMonths(1) && d.SavingAmount > 0).ToList();
            k.SavingMonth = latest.Sum(d => d!.SavingAmount ?? 0m);
            var top = latest.GroupBy(d => (d!.StdIngredientId, d.SubIngredientId)).OrderByDescending(g => g.Sum(x => x!.SavingAmount ?? 0m)).FirstOrDefault();
            if (top != null)
            {
                var ids = new[] { top.Key.StdIngredientId, top.Key.SubIngredientId };
                var names = await db.Ingredient.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
                k.SavingNote = $"{names.GetValueOrDefault(top.Key.SubIngredientId)} thay {names.GetValueOrDefault(top.Key.StdIngredientId)} · {Qty(top.Sum(x => x!.SubQuantity ?? 0m))}";
            }
            else
            {
                k.SavingNote = "chưa có ngày thay thế";
            }
        }

        // ------------------------------------------------------------------ chi tiết ô

        public async Task<IResult<PurchasePlanCellDetailModel>> GetCell(PurchasePlanCellRequest request)
        {
            try
            {
                var db = unitOfWork.DbContext;
                DateTime today = clock.Today, date = request.NeedDate.Date;
                var ing = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.IngredientId);
                if (ing == null)
                {
                    return Result<PurchasePlanCellDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var board = await BuildBoardAsync(new PurchasePlanBoardRequest { From = date, Days = 1, OrganizationUnitId = request.OrganizationUnitId }, false);
                MaterialHorizonModel? hz = null;
                if (date > today)
                {
                    // Kế hoạch tính lũy kế từ ngày mai tới ngày cần (đúng như job) + lượng theo món của ngày cần.
                    (hz, _) = await materialRequirementService.PlanHorizonAsync(new MaterialHorizonRequest { Days = (date - today).Days, OrganizationUnitId = request.OrganizationUnitId }, null, date);
                }
                var sub = hz?.ListSubstitution.FirstOrDefault(s => s.Date.Date == date && (s.StdIngredientId == ing.Id || s.SubIngredientId == ing.Id));
                var group = sub == null ? [ing.Id] : new List<Guid> { sub.StdIngredientId, sub.SubIngredientId };
                var ings = await db.Ingredient.AsNoTracking().Where(i => group.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var row = board?.ListRow.FirstOrDefault(r => r.IngredientId == ing.Id);
                var cell = row?.ListCell.FirstOrDefault();
                var m = new PurchasePlanCellDetailModel
                {
                    IngredientId = ing.Id, IngredientName = ing.Name, UnitName = row?.UnitName, NeedDate = date, Status = cell?.Status, StatusName = cell?.StatusName, Substitution = sub,
                };
                var item = hz?.ListItem.FirstOrDefault(i => i.IngredientId == ing.Id);
                var hzCell = item?.ListDay.FirstOrDefault(c => c.Date.Date == date);
                m.OrderByDate = hzCell?.OrderByDate ?? cell?.OrderByDate;
                string groupName = sub == null ? ing.Name ?? string.Empty : FirstWord(ings.GetValueOrDefault(sub.StdIngredientId)?.Name, ings.GetValueOrDefault(sub.SubIngredientId)?.Name);
                m.Title = $"{groupName} – cần ngày {date:dd/MM}" + (cell?.StatusName is { Length: > 0 } sn ? " · " + sn.ToLowerInvariant() : string.Empty);
                m.Subtitle = hzCell?.LeadTimeDays is { } lt
                    ? $"NCC giao trước {lt} ngày + dự phòng {_rules.PurchaseOrderLeadBufferDays} ngày → hạn đặt {(m.OrderByDate ?? date):dd/MM}. Hệ thống tự tính lúc {_plan.RunTime(0)} và {_plan.RunTime(1)}."
                    : $"Hệ thống tự tính lúc {_plan.RunTime(0)} và {_plan.RunTime(1)}.";

                // Bước 1 – nhu cầu theo công thức.
                foreach (var g in (hz?.ListDishUse ?? []).Where(u => group.Contains(u.IngredientId)).GroupBy(u => u.DishId))
                {
                    var ds = sub?.ListDish.FirstOrDefault(d => d.DishId == g.Key);
                    var std = g.FirstOrDefault(u => u.IngredientId == group[0]);
                    var alt = sub == null ? null : g.FirstOrDefault(u => u.IngredientId == sub.SubIngredientId);
                    m.ListDish.Add(new PurchasePlanDishLineModel
                    {
                        DishName = g.First().DishName, Portions = g.Max(u => u.Portions),
                        StandardText = std == null ? "–" : $"{ings.GetValueOrDefault(group[0])?.Name} {Qty(std.Gross)}",
                        SubstituteText = sub == null ? null : ds == null ? "công thức không cho thay" : alt == null || alt.Gross == 0
                            ? (ds.StdOnlyPortions >= ds.Portions && ds.Portions > 0 ? "khách chỉ dùng chuẩn – không thay" : "giữ chuẩn")
                            : $"{sub.SubIngredientName} {Qty(alt.Gross)} ({ds.EffectivePct:0.#}%)",
                    });
                    if (ds is { StdOnlyPortions: > 0 })
                    {
                        m.ListDish.Add(new PurchasePlanDishLineModel { DishName = "↳ trong đó khách chỉ dùng chuẩn", Portions = ds.StdOnlyPortions, StandardText = "giữ chuẩn", SubstituteText = "không thay", StandardOnly = true });
                    }
                }

                // Bước 3 – trừ hàng đã có (theo ô kế hoạch: lũy kế tới ngày).
                foreach (Guid id in group)
                {
                    var it = hz?.ListItem.FirstOrDefault(i => i.IngredientId == id);
                    var c = it?.ListDay.FirstOrDefault(x => x.Date.Date == date);
                    m.ListNet.Add(new PurchasePlanNetModel
                    {
                        IngredientId = id, IngredientName = ings.GetValueOrDefault(id)?.Name, UnitName = it?.UnitName, Required = c?.CumRequired ?? 0m, SafetyStock = c?.SafetyStock ?? 0m,
                        Available = c?.Available ?? 0m, Incoming = c?.Incoming ?? 0m, Requested = c?.Requested ?? 0m, ToBuy = c?.Shortage ?? 0m,
                    });
                }

                // Bước 4 – chia NCC (theo quy tắc gợi ý chung: tỷ lệ + lũy kế tháng / 1 NCC).
                var (pos, prs) = await OrdersAsync(date, date);
                foreach (var n in m.ListNet)
                {
                    decimal qty = n.ToBuy > 0 ? n.ToBuy : prs.Where(p => p.IngredientId == n.IngredientId).Sum(p => p.Quantity);
                    if (qty <= 0)
                    {
                        continue;
                    }
                    var i = ings.GetValueOrDefault(n.IngredientId);
                    Guid unit = i?.BaseUnitId is { } b && b != Guid.Empty ? b : i?.BaseUomId ?? Guid.Empty;
                    var key = Guid.NewGuid();
                    var pick = (await SupplierPick.SuggestAsync(db, uomConversionService, _rules, today, [new SupplierPick.Need(key, n.IngredientId, qty, unit, date)], _plan.SplitMaxCorrectionPct))[key];
                    var split = new PurchasePlanSplitModel { IngredientId = n.IngredientId, IngredientName = n.IngredientName, UnitName = n.UnitName, Quantity = qty, Manual = pick.SplitManual };
                    split.ListPart = pick.ListSplit.Count > 0 ? pick.ListSplit
                        : pick.SupplierId == null ? []
                        : [new SupplierSplitModel { SupplierId = pick.SupplierId.Value, SupplierName = pick.SupplierName, PriceId = pick.PriceId, UnitPrice = pick.UnitPrice, TargetPct = 100m, Pct = 100m, Quantity = pick.OrderQuantity ?? qty,
                            IsPreferred = pick.ListCard.Any(c => c.Suggested && c.IsPreferred), LeadTimeDays = pick.ListCard.FirstOrDefault(c => c.Suggested)?.LeadTimeDays }];
                    split.Note = pick.ListSplit.FirstOrDefault()?.Note ?? (pick.SupplierId == null ? "Cần chọn NCC – " + pick.Reason : pick.ListSplit.Count == 0 ? "1 NCC (chưa khai tỷ lệ chia)" : null);
                    m.ListSplit.Add(split);
                }

                // Bước 5 – kết quả đơn mua (đã có) hoặc dự kiến.
                var supIds = pos.Select(p => p.SupplierId).Distinct().ToList();
                var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
                var mine = pos.Where(p => group.Contains(p.IngredientId)).ToList();
                foreach (var g in mine.GroupBy(p => p.PoId))
                {
                    var f = g.First();
                    m.ListOrder.Add(new PurchasePlanOrderModel
                    {
                        PurchaseOrderId = f.PoId, PurchaseOrderNumber = f.Number, PurchaseRequestId = f.PrId, SupplierName = sups.GetValueOrDefault(f.SupplierId),
                        Content = string.Join(", ", g.Select(x => $"{ings.GetValueOrDefault(x.IngredientId)?.Name} {Qty(x.Quantity)}")), Amount = g.Sum(x => x.Amount),
                        ApprovalText = f.Note?.Contains(PurchasePlanRules.AutoApprovedNote, StringComparison.Ordinal) == true ? (sub != null && f.IngredientId == sub.SubIngredientId ? "Tự duyệt – thay trong giới hạn công thức" : "Tự duyệt – đúng định mức")
                            : PurchaseOrderStatus.Label(f.Status),
                        SendText = f.Status == PurchaseOrderStatus.Approved ? $"Chờ gửi {_plan.SendTime}" : f.Status is PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received ? "Đã gửi" : "–",
                    });
                }
                foreach (var p in prs.Where(p => group.Contains(p.IngredientId)).GroupBy(p => p.PrId))
                {
                    m.ListOrder.Add(new PurchasePlanOrderModel
                    {
                        PurchaseRequestId = p.Key, PurchaseRequestNumber = p.First().Number, Content = string.Join(", ", p.Select(x => $"{ings.GetValueOrDefault(x.IngredientId)?.Name} {Qty(x.Quantity)}")),
                        ApprovalText = PurchaseRequestStatus.Label(p.First().Status) + (p.First().Note?.Contains("Cần duyệt:", StringComparison.Ordinal) == true ? " – " + Trim(p.First().Note![(p.First().Note!.IndexOf("Cần duyệt:", StringComparison.Ordinal) + 10)..], 80) : string.Empty),
                        SendText = "–",
                    });
                }
                if (m.ListOrder.Count == 0)
                {
                    foreach (var s in m.ListSplit)
                    {
                        foreach (var part in s.ListPart)
                        {
                            m.ListOrder.Add(new PurchasePlanOrderModel
                            {
                                Planned = true, SupplierName = part.SupplierName, Content = $"{s.IngredientName} {Qty(part.Quantity)} {s.UnitName}", Amount = Math.Round(part.Quantity * (part.UnitPrice ?? 0m), 0),
                                ApprovalText = s.Manual ? "Cần Trưởng mua hàng duyệt – sửa phần chia" : "Dự kiến tự duyệt nếu đúng định mức", SendText = $"Gửi {_plan.SendTime}",
                            });
                        }
                    }
                }
                m.CanOrder = m.ListNet.Any(n => n.ToBuy > 0);
                if (sub != null && sub.ListDish.Any(d => d.SubGross > 0))
                {
                    m.Message = $"Kế hoạch sản xuất và phiếu xuất kho ngày {date:dd/MM} ghi rõ "
                        + string.Join("; ", sub.ListDish.Where(d => d.SubGross > 0).Select(d => $"{d.DishName}: {sub.StdIngredientName} {Qty(d.StdGross)} + {sub.SubIngredientName} {Qty(d.SubGross)}"))
                        + "; bếp nhận thông báo qua bot. Giá vốn suất ăn tính theo thực tế.";
                }
                return Result<PurchasePlanCellDetailModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCell: ", ex.ToString()));
                throw;
            }
        }

        private static string FirstWord(string? a, string? b)
        {
            var wa = (a ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wb = (b ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int n = 0;
            while (n < wa.Length && n < wb.Length && string.Equals(wa[n], wb[n], StringComparison.OrdinalIgnoreCase)) n++;
            return n > 0 ? string.Join(' ', wa.Take(n)) : $"{a} / {b}";
        }
    }
}
