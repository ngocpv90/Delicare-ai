using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.5 (D-84, anh chốt 2026-11-23): Dự báo theo TUẦN – mọi khách có hợp đồng hiệu lực trong tuần tự hiện (điểm giao + loại suất theo hợp đồng),
    /// một số mỗi ô: Khách đã đăng ký (đơn Đã xác nhận trở lên – chỉ đọc) &gt; Nháp &gt; Đã công bố &gt; gợi ý / thiếu dữ liệu; ngoài hiệu lực hợp đồng = khóa.
    /// Sửa ô = lưu Nháp ngay (<see cref="SaveCell"/>); Công bố theo tuần chỉ ô Nháp, bỏ qua ô đã đăng ký (<see cref="PublishWeek"/>).
    /// Không thêm bảng / cột (GAP-FC-01: nguồn gợi ý vẫn đánh dấu trong Note).
    /// </summary>
    public partial class ForecastService
    {
        public const string CellPublished = "PUBLISHED", CellDraft = "DRAFT", CellRegistered = "REGISTERED", CellMissing = "MISSING", CellLocked = "LOCKED", CellEmpty = "EMPTY";

        /// <summary>Đơn coi là khách đã đăng ký (D-84 e): Đã xác nhận trở lên.</summary>
        private static readonly string[] RegisteredStatuses = [OrderStatus.Confirmed, OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled];

        public static DateTime MondayOf(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

        private DateTime WeekOf(DateTime? weekStart) => MondayOf(weekStart ?? WeekRules.NextMonday(_clock.Today));

        /// <summary>Số suất khách đã đăng ký theo (khách, điểm giao, ngày, loại suất) – Σ dòng đơn Đã xác nhận trở lên.</summary>
        private async Task<Dictionary<(Guid, Guid?, DateTime, string), decimal>> RegisteredAsync(DateTime monday, IReadOnlyCollection<Guid> customerIds)
        {
            var db = _unitOfWork.DbContext;
            DateTime end = monday.AddDays(7);
            var rows = await (from o in db.Order.AsNoTracking()
                              join l in db.OrderLine.AsNoTracking() on o.Id equals l.OrderId
                              where !o.IsDeleted && !l.IsDeleted && customerIds.Contains(o.CustomerId) && o.MealDate >= monday && o.MealDate < end && RegisteredStatuses.Contains(o.Status!)
                              select new { o.CustomerId, o.DeliveryLocationId, o.MealDate, l.MealType, l.Quantity }).ToListAsync();
            return rows.GroupBy(r => (r.CustomerId, r.DeliveryLocationId, r.MealDate.Date, Meal(r.MealType))).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        }

        public async Task<IResult<ForecastWeekModel>> GetWeek(ForecastWeekRequest request)
        {
            try
            {
                DateTime monday = WeekOf(request.WeekStart), sunday = monday.AddDays(6), end = monday.AddDays(7);
                var (cells, customers, locations) = await ComputeSuggestionsAsync(monday, sunday, null, request.BranchId);
                var model = new ForecastWeekModel { WeekStart = monday, ListDate = [.. Enumerable.Range(0, 7).Select(i => monday.AddDays(i))], LookbackWeeks = Math.Max(1, _rules.ForecastLookbackWeeks) };
                var ids = customers.Keys.ToList();
                var rows = await _unitOfWork.DbContext.Forecast.AsNoTracking()
                    .Where(f => !f.IsDeleted && ids.Contains(f.CustomerId) && f.ForecastDate >= monday && f.ForecastDate < end && f.ShiftId == null
                        && (f.Status == ForecastStatus.Draft || f.Status == ForecastStatus.Published)).ToListAsync();
                var registered = await RegisteredAsync(monday, ids);
                var cellOf = cells.ToDictionary(c => (c.Key.CustomerId, c.Key.LocationId, c.Key.Date, c.Key.MealType));
                foreach (var cust in cells.GroupBy(c => c.Key.CustomerId).OrderBy(g => customers.GetValueOrDefault(g.Key).Name, StringComparer.CurrentCulture))
                {
                    var (code, name) = customers.GetValueOrDefault(cust.Key);
                    var cm = new ForecastWeekCustomerModel { CustomerId = cust.Key, CustomerCode = code, CustomerName = name, ListTotal = [.. Enumerable.Repeat(0m, 7)] };
                    foreach (var row in cust.GroupBy(c => (c.Key.LocationId, c.Key.MealType))
                        .OrderBy(g => g.Key.LocationId is { } l ? locations.GetValueOrDefault(l) : string.Empty, StringComparer.CurrentCulture).ThenBy(g => MealRankOf(g.Key.MealType)))
                    {
                        var rm = new ForecastWeekRowModel
                        {
                            DeliveryLocationId = row.Key.LocationId, MealType = row.First().MealType ?? row.Key.MealType,
                            DeliveryLocationName = row.Key.LocationId is { } loc ? locations.GetValueOrDefault(loc) : "(chung)",
                        };
                        for (int i = 0; i < 7; i++)
                        {
                            DateTime day = monday.AddDays(i);
                            var cell = new ForecastWeekCellModel { Date = day };
                            if (!cellOf.TryGetValue((cust.Key, row.Key.LocationId, day, row.Key.MealType), out var sc))
                            {
                                cell.Status = CellLocked;
                                rm.ListCell.Add(cell);
                                continue;
                            }
                            cell.Suggested = sc.Suggested;
                            cell.Average = sc.Average is { } a ? Math.Round(a, 1) : null;
                            cell.ListHistory = [.. sc.History.Select(h => h.Actual is { } v ? (decimal?)Math.Round(v, 0) : null)];
                            cell.ListHistoryDate = [.. sc.History.Select(h => h.Date)];
                            var same = rows.Where(f => f.CustomerId == cust.Key && f.DeliveryLocationId == row.Key.LocationId && f.ForecastDate.Date == day && Meal(f.MealType) == row.Key.MealType).ToList();
                            var pub = same.Where(f => f.Status == ForecastStatus.Published).OrderByDescending(f => f.VersionNo).FirstOrDefault();
                            var draft = same.Where(f => f.Status == ForecastStatus.Draft).OrderByDescending(f => f.VersionNo).FirstOrDefault();
                            cell.Published = pub?.ForecastQuantity;
                            cell.Draft = draft?.ForecastQuantity;
                            cell.DraftEdited = draft != null && IsEditedDraft(draft);
                            if (registered.TryGetValue((cust.Key, row.Key.LocationId, day, row.Key.MealType), out decimal reg))
                            {
                                cell.Registered = reg;
                                cell.Status = CellRegistered;
                                cell.Value = reg;
                                model.RegisteredCells++;
                            }
                            else if (draft != null)
                            {
                                cell.Status = CellDraft;
                                cell.Value = draft.ForecastQuantity;
                                model.DraftCells++;
                            }
                            else if (pub != null)
                            {
                                cell.Status = CellPublished;
                                cell.Value = pub.ForecastQuantity;
                                model.PublishedCells++;
                            }
                            else if (sc.Insufficient)
                            {
                                cell.Status = CellMissing;
                                model.MissingCells++;
                            }
                            else
                            {
                                cell.Status = CellEmpty;
                            }
                            if (cell.Value is { } v)
                            {
                                cm.ListTotal[i] += v;
                            }
                            rm.ListCell.Add(cell);
                        }
                        cm.ListRow.Add(rm);
                    }
                    model.ListCustomer.Add(cm);
                }
                model.ListTotal = [.. Enumerable.Range(0, 7).Select(i => model.ListCustomer.Sum(c => c.ListTotal[i]))];
                model.TotalWeek = model.ListTotal.Sum();
                model.Mape4Weeks = await MapeAsync(monday.AddDays(-28), monday.AddDays(-1), request.BranchId);
                return Result<ForecastWeekModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeek: ", ex.ToString()));
                throw;
            }
        }

        private static int MealRankOf(string? meal) => ForecastRules.Norm(meal) switch { "SANG" => 0, "TRUA" => 1, "CHIEU" => 2, "TOI" => 3, "DEM" => 4, _ => 9 };

        /// <summary>Sai số bình quân (MAPE %) của dự báo Đã công bố so với suất thực nhận trong khoảng – null khi chưa có ô so được.</summary>
        private async Task<decimal?> MapeAsync(DateTime from, DateTime to, Guid? branchId)
        {
            var acc = await GetAccuracy(new ForecastAccuracyRequest { From = from, To = to, BranchId = branchId });
            return acc.ResponseCode == 200 && acc.Data != null ? acc.Data.ErrorPct : null;
        }

        public async Task<IResult<bool>> SaveCell(ForecastCellRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                DateTime day = request.Date.Date;
                string meal = Meal(request.MealType);
                if (string.IsNullOrEmpty(meal) || request.CustomerId == Guid.Empty)
                {
                    return Fail(ConstantHelper.Message.ForecastCellInvalid);
                }
                if (day < _clock.Today)
                {
                    return Fail(ConstantHelper.Message.ForecastCellPast);
                }
                var registered = await RegisteredAsync(MondayOf(day), [request.CustomerId]);
                if (registered.ContainsKey((request.CustomerId, request.DeliveryLocationId, day, meal)))
                {
                    return Fail(ConstantHelper.Message.ForecastCellRegistered);
                }
                var same = _unitOfWork.ForecastRepository.GetAll()
                    .Where(f => !f.IsDeleted && f.CustomerId == request.CustomerId && f.DeliveryLocationId == request.DeliveryLocationId && f.ForecastDate == day && f.ShiftId == null)
                    .ToList().Where(f => Meal(f.MealType) == meal).ToList();
                var draft = same.Where(f => f.Status == ForecastStatus.Draft).OrderByDescending(f => f.VersionNo).FirstOrDefault();
                if (request.Quantity is not { } qty || qty < 0)
                {
                    if (draft != null)
                    {
                        draft.IsDeleted = true;
                        _unitOfWork.ForecastRepository.Update(draft);
                        await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                    }
                    _unitOfWork.CommitTransaction();
                    return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
                }
                if (draft != null)
                {
                    draft.ForecastQuantity = qty;
                    draft.Quantity = qty;
                    _unitOfWork.ForecastRepository.Update(draft);
                    await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                else
                {
                    _unitOfWork.ForecastRepository.Add(new Forecast
                    {
                        Id = Guid.NewGuid(), CustomerId = request.CustomerId, DeliveryLocationId = request.DeliveryLocationId, ForecastDate = day,
                        MealType = same.FirstOrDefault()?.MealType ?? request.MealType!.Trim(), Quantity = qty, ForecastQuantity = qty, Status = ForecastStatus.Draft,
                        VersionNo = same.Count == 0 ? 1 : same.Max(f => f.VersionNo) + 1,
                    });
                    await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                }
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveCell: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<bool> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<bool>.Fail(message);
            }
        }

        /// <summary>Tạo gợi ý cho tuần (mặc định tuần sau, tùy chọn một khách) – giữ ô người đã sửa (D-84; FC-1).</summary>
        public Task<IResult<ForecastSuggestResultModel>> SuggestWeek(ForecastWeekActionRequest request)
        {
            DateTime monday = WeekOf(request.WeekStart);
            return Suggest(new ForecastSuggestRequest { From = monday, To = monday.AddDays(6), CustomerId = request.CustomerId });
        }

        /// <summary>
        /// Công bố tuần: mọi ô Nháp (hoặc của 1 khách) – bỏ qua ô khách đã đăng ký (số đăng ký dùng thay dự báo); bản cũ cùng khóa → Bị thay thế.
        /// <paramref name="auto"/> = job thứ Sáu 16:00 – ghi chú "Hệ thống tự công bố" và trả danh sách ô thiếu dữ liệu để báo KD.
        /// </summary>
        public async Task<IResult<ForecastWeekPublishResultModel>> PublishWeek(ForecastWeekActionRequest request, bool auto = false)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                DateTime monday = WeekOf(request.WeekStart), end = monday.AddDays(7);
                var rows = _unitOfWork.ForecastRepository.GetAll()
                    .Where(f => !f.IsDeleted && f.ForecastDate >= monday && f.ForecastDate < end && f.ShiftId == null
                        && (f.Status == ForecastStatus.Draft || f.Status == ForecastStatus.Published)
                        && (request.CustomerId == null || f.CustomerId == request.CustomerId)).ToList();
                var registered = await RegisteredAsync(monday, [.. rows.Select(r => r.CustomerId).Distinct()]);
                var result = new ForecastWeekPublishResultModel { WeekStart = monday };
                foreach (var group in rows.GroupBy(ForecastRules.KeyOf))
                {
                    var draft = group.Where(f => f.Status == ForecastStatus.Draft).OrderByDescending(f => f.VersionNo).FirstOrDefault();
                    if (draft == null)
                    {
                        continue;
                    }
                    if (registered.ContainsKey((draft.CustomerId, draft.DeliveryLocationId, draft.ForecastDate.Date, Meal(draft.MealType))))
                    {
                        result.SkippedRegistered++;
                        continue;
                    }
                    foreach (var other in group.Where(f => f != draft && f.VersionNo <= draft.VersionNo))
                    {
                        other.Status = ForecastStatus.Superseded;
                        _unitOfWork.ForecastRepository.Update(other);
                        result.Superseded++;
                    }
                    draft.Status = ForecastStatus.Published;
                    if (auto)
                    {
                        draft.Note = string.IsNullOrWhiteSpace(draft.Note) ? "Hệ thống tự công bố" : draft.Note + " | Hệ thống tự công bố";
                    }
                    _unitOfWork.ForecastRepository.Update(draft);
                    result.Published++;
                }
                if (auto)
                {
                    var week = await GetWeek(new ForecastWeekRequest { WeekStart = monday });
                    if (week.Data != null)
                    {
                        foreach (var c in week.Data.ListCustomer)
                        {
                            foreach (var r in c.ListRow)
                            {
                                foreach (var cell in r.ListCell.Where(x => x.Status == CellMissing))
                                {
                                    result.Missing++;
                                    if (result.ListMissing.Count < 30)
                                    {
                                        result.ListMissing.Add($"{c.CustomerName} · {r.DeliveryLocationName} · {r.MealType} · {cell.Date:dd/MM}");
                                    }
                                }
                            }
                        }
                    }
                }
                if (result.Published == 0 && !auto)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ForecastWeekPublishResultModel>.Fail(ConstantHelper.Message.ForecastNothingToPublish);
                }
                if (result.Published > 0)
                {
                    await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
                return Result<ForecastWeekPublishResultModel>.Success(result, string.Format(ConstantHelper.Message.ForecastWeekPublished, result.Published, monday.ToString("dd/MM")));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PublishWeek: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
