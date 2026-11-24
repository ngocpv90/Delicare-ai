using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Services.Implement.Messaging;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.30 (D-111): "Kiểm tra theo ca / mẻ" – ma trận Bộ kiểm tra × Ca của một ngày, Sổ kiểm tra (kiểm thực bước 2, chỉ xem), tỷ lệ đạt theo bộ.
    /// Chỉ các bộ ghi được qua Record (bỏ bộ công đoạn / lưu mẫu / vệ sinh khay – FoodSafetyRules.ViaAction). Không thêm bảng / cột.
    /// </summary>
    public partial class FoodSafetyChecklistResultService
    {
        private const int BookMaxDays = 93;

        /// <summary>Bộ đang áp dụng ngày <paramref name="date"/> ghi được ở màn (mỗi mã một phiên bản – lớn nhất).</summary>
        private async Task<List<FoodSafetyChecklist>> BoardChecklistsAsync(DateTime date)
            => (await _unitOfWork.DbContext.FoodSafetyChecklist.AsNoTracking().Where(c => !c.IsDeleted).ToListAsync())
                .Where(c => FoodSafetyRules.IsEffective(c, date) && FoodSafetyRules.ViaAction(c.Code, _rules) == null)
                .GroupBy(c => c.Code ?? c.Id.ToString()).Select(g => g.OrderByDescending(c => c.VersionNo).First())
                .OrderBy(c => _rules.ChecklistOffsetsMinutes.TryGetValue(c.Code ?? string.Empty, out int o) ? o : int.MaxValue).ThenBy(c => c.Name).ToList();

        private static string Hm(TimeSpan? t) => t is { } v ? $"{(int)v.TotalHours % 24:00}:{v.Minutes:00}" : string.Empty;

        private static bool InShift(Shift s, TimeSpan now)
            => s.StartTime is { } a && s.EndTime is { } b && (a <= b ? now >= a && now < b : now >= a || now < b);

        public async Task<IResult<ChecklistDayBoardModel>> GetDayBoard(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Today).Date, now = _clock.Now;
                var shifts = (await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToListAsync())
                    .Where(s => !string.Equals(s.Status, "Inactive", StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.StartTime ?? TimeSpan.MaxValue).ThenBy(s => s.Code).ToList();
                var checklists = await BoardChecklistsAsync(day);
                var clIds = checklists.Select(c => c.Id).ToList();
                var items = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => !i.IsDeleted && clIds.Contains(i.ChecklistId)).ToListAsync();
                var results = await db.FoodSafetyChecklistResult.AsNoTracking()
                    .Where(r => !r.IsDeleted && r.CheckDate == day && clIds.Contains(r.ChecklistId))
                    .OrderBy(r => r.CheckedAt).ThenBy(r => r.CreatedDate).ToListAsync();
                // Mẻ của ngày theo ca (lệnh SX của kế hoạch ngày).
                var open = new[] { ProductionBatchStatus.Planned, ProductionBatchStatus.Cooking, ProductionBatchStatus.QcPending, ProductionBatchStatus.Passed, ProductionBatchStatus.Packed };
                var batches = await (from b in db.ProductionBatch.AsNoTracking()
                                     join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id
                                     join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id
                                     join d in db.Dish.AsNoTracking() on b.DishId equals d.Id into dj
                                     from d in dj.DefaultIfEmpty()
                                     where !b.IsDeleted && !o.IsDeleted && !p.IsDeleted && p.ProductionDate == day && b.Status != null && open.Contains(b.Status)
                                     select new { b.Id, Number = b.BatchNumber ?? b.BatchNo, b.Status, o.ShiftId, DishName = d != null ? d.Name : null }).ToListAsync();
                var batchNo = batches.ToDictionary(b => b.Id, b => b.Number);
                var extraIds = results.Where(r => r.ProductionBatchId != null && !batchNo.ContainsKey(r.ProductionBatchId.Value)).Select(r => r.ProductionBatchId!.Value).Distinct().ToList();
                foreach (var x in await db.ProductionBatch.AsNoTracking().Where(b => extraIds.Contains(b.Id)).Select(b => new { b.Id, N = b.BatchNumber ?? b.BatchNo }).ToListAsync())
                {
                    batchNo[x.Id] = x.N;
                }
                var model = new ChecklistDayBoardModel { Date = day };
                if (day == now.Date)
                {
                    model.CurrentShiftId = shifts.FirstOrDefault(s => InShift(s, now.TimeOfDay))?.Id;
                }
                foreach (var s in shifts)
                {
                    model.ListShift.Add(new ChecklistBoardShiftModel
                    {
                        Id = s.Id, Code = s.Code, Name = s.Name, StartTime = Hm(s.StartTime), EndTime = Hm(s.EndTime), IsCurrent = s.Id == model.CurrentShiftId,
                        ListBatch = [.. batches.Where(b => b.ShiftId == s.Id).OrderBy(b => b.Number).Select(b => new ChecklistBoardBatchModel
                        {
                            Id = b.Id, BatchNumber = b.Number, DishName = b.DishName, Status = b.Status, StatusName = ProductionBatchStatus.Label(b.Status),
                            Suggested = b.Status is ProductionBatchStatus.Cooking or ProductionBatchStatus.QcPending,
                        })],
                    });
                }
                var dispatch = new Dictionary<Guid, (DateTime? At, string Source)>();
                foreach (var s in shifts)
                {
                    dispatch[s.Id] = await ProductionRules.ResolveDispatchAsync(db, day, s, null, _rules);
                }
                foreach (var c in checklists)
                {
                    var its = items.Where(i => i.ChecklistId == c.Id).ToList();
                    var row = new ChecklistBoardRowModel { ChecklistId = c.Id, Code = c.Code, Name = c.Name, ItemCount = its.Count, RequiredCount = its.Count(i => i.IsRequired) };
                    foreach (var s in shifts)
                    {
                        var run = FoodSafetyRules.Latest(results.Where(r => r.ChecklistId == c.Id && r.ShiftId == s.Id && r.ProductionBatchId == null), r => r.ChecklistItemId);
                        var cell = new ChecklistBoardCellModel
                        {
                            ShiftId = s.Id, Total = its.Count, Done = its.Count(i => run.ContainsKey(i.Id)),
                            Pass = its.Count(i => run.TryGetValue(i.Id, out var r) && r.ResultCode == FoodSafetyCode.ResultPass),
                            Fail = its.Count(i => run.TryGetValue(i.Id, out var r) && r.ResultCode == FoodSafetyCode.ResultFail),
                        };
                        // Lỗi trọng yếu đang mở: dòng mới nhất Không đạt của tiêu chí trọng yếu – của lượt chung hoặc gắn mẻ cùng ngày / ca.
                        var critical = its.Where(i => i.IsCritical).Select(i => i.Id).ToHashSet();
                        var batchLatest = new Dictionary<(Guid Batch, Guid Item), string?>();
                        foreach (var r in results.Where(r => r.ChecklistId == c.Id && r.ShiftId == s.Id && r.ProductionBatchId != null && critical.Contains(r.ChecklistItemId)))
                        {
                            batchLatest[(r.ProductionBatchId!.Value, r.ChecklistItemId)] = r.ResultCode;
                        }
                        var held = batchLatest.Where(x => x.Value == FoodSafetyCode.ResultFail).Select(x => x.Key.Batch).Distinct().ToList();
                        cell.ListHeldBatch = [.. held.Select(b => batchNo.GetValueOrDefault(b) ?? string.Empty).Where(n => n.Length > 0).OrderBy(n => n)];
                        cell.CriticalFail = its.Count(i => i.IsCritical && run.TryGetValue(i.Id, out var r) && r.ResultCode == FoodSafetyCode.ResultFail)
                                            + batchLatest.Where(x => x.Value == FoodSafetyCode.ResultFail && !(run.TryGetValue(x.Key.Item, out var rr) && rr.ResultCode == FoodSafetyCode.ResultFail)).Select(x => x.Key.Item).Distinct().Count();
                        if (c.Code != null && _rules.ChecklistOffsetsMinutes.TryGetValue(c.Code, out int offset) && dispatch[s.Id].At is { } at)
                        {
                            cell.DueAt = at.AddMinutes(offset);
                            cell.DueSource = $"Giờ xuất bếp {ProductionRules.Time(at)} {(offset >= 0 ? "+" : "−")} {Math.Abs(offset)} phút";
                        }
                        bool complete = its.Count > 0 && its.Where(i => i.IsRequired).All(i => run.ContainsKey(i.Id)) && run.Count > 0;
                        cell.Status = cell.CriticalFail > 0 ? "CRITICAL"
                            : complete ? (cell.Fail > 0 ? "FAIL" : "PASS")
                            : cell.DueAt is { } due && due < now ? "OVERDUE"
                            : cell.DueAt is { } due2 && due2.AddMinutes(-60) > now ? "NOT_DUE"
                            : "DUE";
                        row.ListCell.Add(cell);
                    }
                    model.ListChecklist.Add(row);
                }
                var cells = model.ListChecklist.SelectMany(r => r.ListCell).ToList();
                model.DueCount = cells.Count(x => x.Total > 0);
                model.DoneCount = cells.Count(x => x.Status is "PASS" or "FAIL" || (x.Status == "CRITICAL" && x.Done >= x.Total));
                model.OverdueCount = cells.Count(x => x.Status == "OVERDUE");
                model.OpenCriticalCount = cells.Sum(x => x.CriticalFail);
                return Result<ChecklistDayBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDayBoard: ", ex.ToString()));
                throw;
            }
        }

        private (DateTime From, DateTime To) BookRange(DateTime? from, DateTime? to, int defaultDays)
        {
            DateTime dTo = (to ?? _clock.Today).Date, dFrom = (from ?? dTo.AddDays(-(defaultDays - 1))).Date;
            if (dFrom > dTo)
            {
                (dFrom, dTo) = (dTo, dFrom);
            }
            if ((dTo - dFrom).TotalDays > BookMaxDays)
            {
                dFrom = dTo.AddDays(-BookMaxDays);
            }
            return (dFrom, dTo);
        }

        /// <summary>Kết quả của khoảng ngày (trừ bộ vệ sinh khay – có sổ riêng), gộp các dòng nhân bản theo mẻ bị ảnh hưởng thành một dòng.</summary>
        private async Task<List<ChecklistBookRowModel>> BookRowsAsync(DateTime dFrom, DateTime dTo, Guid? checklistId, Guid? shiftId)
        {
            var db = _unitOfWork.DbContext;
            var rows = await db.FoodSafetyChecklistResult.AsNoTracking()
                .Where(r => !r.IsDeleted && r.CheckDate >= dFrom && r.CheckDate <= dTo && (checklistId == null || r.ChecklistId == checklistId) && (shiftId == null || r.ShiftId == shiftId))
                .OrderBy(r => r.CheckedAt).ThenBy(r => r.CreatedDate).ToListAsync();
            var clIds = rows.Select(r => r.ChecklistId).Distinct().ToList();
            var checklists = await db.FoodSafetyChecklist.AsNoTracking().Where(c => clIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
            var cleaning = checklists.Values.Where(c => string.Equals(c.Code, _rules.CleaningChecklistCode, StringComparison.OrdinalIgnoreCase)).Select(c => c.Id).ToHashSet();
            rows = [.. rows.Where(r => !cleaning.Contains(r.ChecklistId))];
            var itemIds = rows.Select(r => r.ChecklistItemId).Distinct().ToList();
            var items = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var shiftIds = rows.Where(r => r.ShiftId != null).Select(r => r.ShiftId!.Value).Distinct().ToList();
            var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name ?? s.Code);
            var userIds = rows.Where(r => r.CheckedBy != null).Select(r => r.CheckedBy!.Value).Distinct().ToList();
            var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
            var batchIds = rows.Where(r => r.ProductionBatchId != null).Select(r => r.ProductionBatchId!.Value).Distinct().ToList();
            var batches = await db.ProductionBatch.AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BatchNumber ?? b.BatchNo);
            var seen = new HashSet<(Guid, Guid, DateTime, Guid?)>();
            var list = new List<ChecklistBookRowModel>();
            foreach (var g in rows.GroupBy(r => (r.ChecklistId, r.ChecklistItemId, r.CheckDate.Date, r.ShiftId, r.CheckedAt, r.ResultCode, r.CorrectiveAction)))
            {
                var first = g.First();
                items.TryGetValue(first.ChecklistItemId, out var item);
                var key = (first.ChecklistId, first.ChecklistItemId, first.CheckDate.Date, first.ShiftId);
                list.Add(new ChecklistBookRowModel
                {
                    Id = first.Id, CheckDate = first.CheckDate.Date, CheckedAt = first.CheckedAt, ShiftId = first.ShiftId,
                    ShiftName = first.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null, ChecklistId = first.ChecklistId,
                    ChecklistName = checklists.TryGetValue(first.ChecklistId, out var cl) ? cl.Name ?? cl.Code : null,
                    ItemName = item?.ItemName ?? item?.ItemCode, IsCritical = item?.IsCritical ?? false,
                    ExpectedValue = item == null ? null : string.Join(" ", new[] { item.ExpectedValue, item.UnitCode }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    ActualValue = first.ActualValue, ResultCode = first.ResultCode, ResultName = FoodSafetyCode.ResultLabel(first.ResultCode), CorrectiveAction = first.CorrectiveAction,
                    ListBatch = [.. g.Where(r => r.ProductionBatchId != null).Select(r => batches.GetValueOrDefault(r.ProductionBatchId!.Value) ?? string.Empty).Where(n => n.Length > 0).Distinct().OrderBy(n => n)],
                    CheckedByName = first.CheckedBy is { } u ? users.GetValueOrDefault(u) : null,
                    IsRecheck = !seen.Add(key),
                });
            }
            return list;
        }

        public async Task<IResult<ChecklistBookModel>> GetBook(ChecklistBookRequest request)
        {
            try
            {
                request ??= new ChecklistBookRequest();
                var (dFrom, dTo) = BookRange(request.From, request.To, 7);
                string kw = (request.Keyword ?? string.Empty).Trim(), res = (FoodSafetyCode.NormalizeResult(request.Result) ?? string.Empty);
                bool Has(string? v) => v?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false;
                var rows = (await BookRowsAsync(dFrom, dTo, request.ChecklistId, request.ShiftId))
                    .Where(r => kw.Length == 0 || Has(r.ItemName) || Has(r.ChecklistName) || Has(r.CorrectiveAction) || Has(r.ActualValue) || Has(r.CheckedByName) || r.ListBatch.Any(Has))
                    .Where(r => res.Length == 0 || r.ResultCode == res)
                    .Where(r => !request.OnlyCritical || (r.IsCritical && r.ResultCode == FoodSafetyCode.ResultFail))
                    .OrderByDescending(r => r.CheckDate).ThenByDescending(r => r.CheckedAt).ToList();
                static int Runs(IEnumerable<ChecklistBookRowModel> x) => x.Select(r => (r.ChecklistId, r.CheckDate, r.ShiftId)).Distinct().Count();
                var model = new ChecklistBookModel
                {
                    From = dFrom, To = dTo, Runs = Runs(rows), Lines = rows.Count,
                    Fail = rows.Count(r => r.ResultCode == FoodSafetyCode.ResultFail), CriticalFail = rows.Count(r => r.IsCritical && r.ResultCode == FoodSafetyCode.ResultFail),
                    ListDay = [.. rows.GroupBy(r => r.CheckDate).OrderByDescending(g => g.Key).Select(g => new ChecklistBookDayModel
                    {
                        Date = g.Key, Runs = Runs(g), Lines = g.Count(), Fail = g.Count(r => r.ResultCode == FoodSafetyCode.ResultFail), ListRow = [.. g],
                    })],
                };
                return Result<ChecklistBookModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBook: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ChecklistPassRateReportModel>> GetPassRate(DateTime? from, DateTime? to)
        {
            try
            {
                var (dFrom, dTo) = BookRange(from, to, 30);
                // Chỉ kết quả Đạt / Không đạt (bộ công đoạn ghi trạng thái làm việc, không tính tỷ lệ đạt).
                var rows = (await BookRowsAsync(dFrom, dTo, null, null)).Where(r => r.ResultCode is FoodSafetyCode.ResultPass or FoodSafetyCode.ResultFail).ToList();
                var model = new ChecklistPassRateReportModel { From = dFrom, To = dTo };
                for (DateTime d = dFrom; d <= dTo; d = d.AddDays(1))
                {
                    model.ListDate.Add(d);
                }
                // Kết quả mới nhất mỗi (ngày, ca, tiêu chí) của bộ – kiểm lại thay lần trước.
                foreach (var g in rows.GroupBy(r => r.ChecklistId))
                {
                    var last = g.GroupBy(r => (r.CheckDate, r.ShiftId, r.ItemName)).Select(x => x.OrderBy(r => r.CheckedAt).Last()).ToList();
                    int all = last.Count, pass = last.Count(r => r.ResultCode == FoodSafetyCode.ResultPass);
                    model.ListSeries.Add(new ChecklistPassRateSeriesModel
                    {
                        ChecklistId = g.Key, Name = g.First().ChecklistName, RatePct = all == 0 ? null : Math.Round(pass * 100m / all, 1),
                        ListRate = [.. model.ListDate.Select(d => { var x = last.Where(r => r.CheckDate == d).ToList(); return x.Count == 0 ? (decimal?)null : Math.Round(x.Count(r => r.ResultCode == FoodSafetyCode.ResultPass) * 100m / x.Count, 1); })],
                    });
                }
                model.ListSeries = [.. model.ListSeries.OrderBy(s => s.Name)];
                model.ListTopFail = [.. rows.Where(r => r.ResultCode == FoodSafetyCode.ResultFail).GroupBy(r => (r.ChecklistId, r.ItemName))
                    .Select(g => new ChecklistTopFailModel { ItemName = g.Key.ItemName, ChecklistName = g.First().ChecklistName, IsCritical = g.First().IsCritical, FailCount = g.Count(), LastDate = g.Max(r => r.CheckDate) })
                    .OrderByDescending(x => x.FailCount).ThenByDescending(x => x.LastDate).Take(10)];
                return Result<ChecklistPassRateReportModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPassRate: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// UI-6.30 (D-111): lượt kiểm tra (bộ × ca) hôm nay quá hạn chưa làm xong → 1 tin gộp theo ca tới người có quyền màn Kiểm tra theo ca / mẻ
        /// (trưởng ca, QA). Khóa idempotent theo (ngày, ca, tập bộ quá hạn) – gọi mỗi vòng BotWatch không gửi trùng.
        /// </summary>
        public async Task<int> RemindOverdueAsync(DateTime now)
        {
            var board = (await GetDayBoard(now.Date)).Data;
            if (board == null)
            {
                return 0;
            }
            int sent = 0;
            foreach (var shift in board.ListShift)
            {
                var late = board.ListChecklist.Select(c => (c, cell: c.ListCell.First(x => x.ShiftId == shift.Id))).Where(x => x.cell.Status == "OVERDUE").ToList();
                if (late.Count == 0)
                {
                    continue;
                }
                string ids = string.Join(".", late.Select(x => x.c.ChecklistId.ToString("N")[..6]).OrderBy(x => x));
                sent += await BusinessNotifier.ToMenusAsync(_notifications, _unitOfWork.DbContext, NotificationCatalog.ChecklistDue, ["#/qc-checklist-result"],
                    new
                    {
                        title = $"Ca {shift.Name ?? shift.Code}: {late.Count} lượt kiểm tra ATTP quá hạn",
                        detail = "Mở Kiểm tra theo ca / mẻ để ghi kết quả",
                        lines = late.Select(x => $"⏰ {x.c.Name} – hạn {x.cell.DueAt:HH:mm}, đã ghi {x.cell.Done}/{x.cell.Total}").ToList(),
                    },
                    $"{NotificationCatalog.ChecklistDue}:{now:yyyyMMdd}:{shift.Id:N}:{ids}");
            }
            return sent;
        }
    }
}
