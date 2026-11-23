using System.Globalization;
using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// FC-1 (D-76, anh chốt 2026-11-23): dự báo do hệ thống tự gợi ý → Kinh doanh sửa chỗ cần → Công bố. Không thêm bảng / cột (GAP-FC-01):
    /// gợi ý ghi thành bản <b>Nháp</b> của <c>Forecast</c>, nguồn đánh dấu trong <c>Note</c> = <c>[GOI_Y=&lt;số&gt;] TB 4 tuần cùng thứ: …</c>.
    /// <list type="bullet">
    /// <item>Khóa = (khách, điểm giao, ngày, loại suất), ca trống (dự báo theo buổi – Σ các lựa chọn, D-74).</item>
    /// <item>Số gợi ý = trung bình "suất thực nhận" cùng thứ trong <c>ForecastLookbackWeeks</c> (4) tuần gần nhất trước hôm nay, làm tròn lên;
    ///   tuần không có đơn ngày đó không tính; &lt; 2 tuần có dữ liệu → "Thiếu dữ liệu", không gợi ý.</item>
    /// <item>Suất thực nhận của đơn (Đã chốt / Đang SX / Đã giao / Đã đối soát): suất tính tiền trong đối soát (BIZ-1.4; đối soát cũ: suất phục vụ)
    ///   → suất khách nhận ở POD → số đặt; chia cho các loại suất theo tỷ lệ số đặt của dòng đơn.</item>
    /// <item>Chỉ (khách, loại suất) có trong hợp đồng hiệu lực tại ngày đó; điểm giao đang hoạt động.</item>
    /// </list>
    /// Không job tự động (D-45): action bấm tay, hàm viết để <c>AppDelicare.Job</c> gọi lại được.
    /// </summary>
    public partial class ForecastService
    {
        public const string SuggestTag = "[GOI_Y=";
        private static readonly string[] ActualOrderStatuses = [OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled];

        internal sealed record SuggestKey(Guid CustomerId, Guid? LocationId, DateTime Date, string MealType);

        internal sealed class SuggestCell
        {
            public required SuggestKey Key { get; init; }
            public string? MealType { get; init; }
            public List<(DateTime Date, decimal? Actual)> History { get; } = [];
            public int WeeksWithData => History.Count(h => h.Actual != null);
            public decimal? Average { get; set; }
            public decimal? Suggested { get; set; }
            public decimal? LastWeek => History.Count == 0 ? null : History[0].Actual;
            public bool Insufficient => Suggested == null;
        }

        private static string Meal(string? s) => ForecastRules.Norm(s);
        private static string Dm(DateTime d) => d.ToString("dd/MM", CultureInfo.InvariantCulture);

        /// <summary>Số suất gợi ý ghi trong Note của bản Nháp do hệ thống tạo (null = bản người nhập).</summary>
        public static decimal? SuggestedOf(string? note)
        {
            if (string.IsNullOrEmpty(note) || !note.StartsWith(SuggestTag, StringComparison.Ordinal))
            {
                return null;
            }
            int end = note.IndexOf(']');
            return end > SuggestTag.Length && decimal.TryParse(note[SuggestTag.Length..end], NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        /// <summary>Bản Nháp người đã sửa (hoặc nhập tay) = không có thẻ gợi ý, hoặc số khác số gợi ý.</summary>
        public static bool IsEditedDraft(Forecast f) => SuggestedOf(f.Note) is not { } s || s != f.ForecastQuantity;

        /// <summary>Mặc định: thứ Hai tuần sau → Chủ nhật tuần kế tiếp (14 ngày).</summary>
        private (DateTime From, DateTime To) DefaultSuggestRange()
        {
            DateTime today = _clock.Today;
            int toMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            DateTime from = today.AddDays(toMonday == 0 ? 7 : toMonday);
            return (from, from.AddDays(13));
        }

        /// <summary>N ngày cùng thứ gần nhất trước hôm nay (mới nhất trước).</summary>
        private List<DateTime> LookbackDates(DateTime target, int weeks)
        {
            DateTime t = _clock.Today.AddDays(-1);
            while (t.DayOfWeek != target.DayOfWeek)
            {
                t = t.AddDays(-1);
            }
            return [.. Enumerable.Range(0, Math.Max(1, weeks)).Select(i => t.AddDays(-7 * i))];
        }

        /// <summary>
        /// Suất thực nhận theo (khách, điểm giao, ngày, loại suất) của các đơn Đã chốt / Đang SX / Đã giao / Đã đối soát trong các ngày cho trước.
        /// Có đơn (kể cả số 0) = có dữ liệu; không có đơn = không có khóa.
        /// </summary>
        internal static async Task<Dictionary<SuggestKey, decimal>> ActualAsync(DatabaseContext db, IReadOnlyCollection<DateTime> dates, IReadOnlyCollection<Guid>? customerIds)
        {
            var result = new Dictionary<SuggestKey, decimal>();
            if (dates.Count == 0)
            {
                return result;
            }
            var daySet = dates.Select(d => d.Date).ToHashSet();
            DateTime min = daySet.Min(), end = daySet.Max().AddDays(1);
            var query = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= min && o.MealDate < end && o.Status != null && ActualOrderStatuses.Contains(o.Status));
            if (customerIds is { Count: > 0 })
            {
                query = query.Where(o => customerIds.Contains(o.CustomerId));
            }
            var orders = (await query.ToListAsync()).Where(o => daySet.Contains(o.MealDate.Date)).ToList();
            if (orders.Count == 0)
            {
                return result;
            }
            var ids = orders.Select(o => o.Id).ToList();
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && ids.Contains(l.OrderId)).ToListAsync();
            var recs = (await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && ids.Contains(r.OrderId)).ToListAsync())
                .GroupBy(r => r.OrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReconciliationDate).ThenByDescending(r => r.CreatedDate).First());
            var deliveries = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && ids.Contains(d.OrderId)).Select(d => new { d.Id, d.OrderId }).ToListAsync();
            var deliveryIds = deliveries.Select(d => d.Id).ToList();
            var pods = await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && deliveryIds.Contains(p.DeliveryOrderId)).ToListAsync();
            var podByOrder = deliveries.GroupJoin(pods, d => d.Id, p => p.DeliveryOrderId, (d, ps) => (d.OrderId, Accepted: ps.Sum(p => p.AcceptedQuantity ?? p.AcceptedQty ?? 0m), Count: ps.Count()))
                .GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => (Accepted: g.Sum(x => x.Accepted), Count: g.Sum(x => x.Count)));
            foreach (var order in orders)
            {
                var own = lines.Where(l => l.OrderId == order.Id && l.Quantity > 0).ToList();
                decimal ordered = own.Sum(l => l.Quantity);
                decimal actual = recs.TryGetValue(order.Id, out var rec) ? BilledOf(rec)
                    : podByOrder.TryGetValue(order.Id, out var pod) && pod.Count > 0 ? pod.Accepted
                    : ordered;
                var meals = own.GroupBy(l => Meal(l.MealType)).ToList();
                if (meals.Count == 0)
                {
                    continue;
                }
                foreach (var g in meals)
                {
                    decimal share = ordered > 0 ? actual * g.Sum(l => l.Quantity) / ordered : 0m;
                    var key = new SuggestKey(order.CustomerId, order.DeliveryLocationId, order.MealDate.Date, g.Key);
                    result[key] = result.GetValueOrDefault(key) + share;
                }
            }
            return result;
        }

        /// <summary>Suất tính tiền ghi trong đối soát (JSON Note – BIZ-1.4); đối soát cũ không có → suất phục vụ.</summary>
        private static decimal BilledOf(OrderReconciliation rec)
        {
            if (!string.IsNullOrWhiteSpace(rec.Note) && rec.Note.TrimStart().StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rec.Note);
                    if (doc.RootElement.TryGetProperty("billingQuantity", out var b) && b.ValueKind == JsonValueKind.Number)
                    {
                        return b.GetDecimal();
                    }
                }
                catch (JsonException)
                {
                    // Note ghi tay cũ – dùng suất phục vụ.
                }
            }
            return rec.AcceptedQuantity;
        }

        /// <summary>Tính gợi ý (không ghi) cho [from, to]: mọi khóa (khách có hợp đồng hiệu lực × điểm giao hoạt động × loại suất trong hợp đồng) × ngày.</summary>
        internal async Task<(List<SuggestCell> Cells, Dictionary<Guid, (string? Code, string? Name)> Customers, Dictionary<Guid, string?> Locations)> ComputeSuggestionsAsync(
            DateTime from, DateTime to, Guid? customerId, Guid? branchId)
        {
            var db = _unitOfWork.DbContext;
            DateTime end = to.Date.AddDays(1);
            var contracts = await db.Contract.AsNoTracking()
                .Where(c => !c.IsDeleted && (c.Status == ContractStatus.Active || c.Status == ContractStatus.Terminated)
                    && c.EffectiveFrom < end && (c.EffectiveTo == null || c.EffectiveTo >= from.Date)
                    && (customerId == null || customerId == Guid.Empty || c.CustomerId == customerId))
                .ToListAsync();
            var customerQuery = db.Customer.AsNoTracking().Where(c => !c.IsDeleted);
            if (branchId is { } bid && bid != Guid.Empty)
            {
                customerQuery = customerQuery.Where(c => c.BranchId == bid);
            }
            var contractCustomerIds = contracts.Select(c => c.CustomerId).Distinct().ToList();
            var customers = await customerQuery.Where(c => contractCustomerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.Code, Name: c.DisplayName ?? c.Name));
            contracts = [.. contracts.Where(c => customers.ContainsKey(c.CustomerId))];
            var contractIds = contracts.Select(c => c.Id).ToList();
            var meals = await db.ContractMeal.AsNoTracking()
                .Where(m => contractIds.Contains(m.ContractId) && !m.IsDeleted && (m.Status == null || m.Status == "Active") && m.MealType != null).ToListAsync();
            var ids = customers.Keys.ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking()
                .Where(l => ids.Contains(l.CustomerId) && !l.IsDeleted && (l.Status == null || l.Status != "Inactive")).ToListAsync();
            var locationNames = locations.ToDictionary(l => l.Id, l => (string?)(l.Name ?? l.Code));

            int weeks = Math.Max(1, _rules.ForecastLookbackWeeks);
            var historyDates = new HashSet<DateTime>();
            var targets = new List<(DateTime Date, List<DateTime> Back)>();
            for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
            {
                var back = LookbackDates(d, weeks);
                targets.Add((d, back));
                historyDates.UnionWith(back);
            }
            var actual = await ActualAsync(db, historyDates, ids);

            var cells = new List<SuggestCell>();
            foreach (var (date, back) in targets)
            {
                foreach (var contract in contracts.Where(c => c.EffectiveFrom.Date <= date && (c.EffectiveTo == null || c.EffectiveTo.Value.Date >= date)))
                {
                    var custLocations = locations.Where(l => l.CustomerId == contract.CustomerId).Select(l => (Guid?)l.Id).ToList();
                    if (custLocations.Count == 0)
                    {
                        custLocations.Add(null);
                    }
                    foreach (var mealType in meals.Where(m => m.ContractId == contract.Id).Select(m => m.MealType!).DistinctBy(Meal))
                    {
                        foreach (var loc in custLocations)
                        {
                            var key = new SuggestKey(contract.CustomerId, loc, date, Meal(mealType));
                            if (cells.Any(c => c.Key == key))
                            {
                                continue;
                            }
                            var cell = new SuggestCell { Key = key, MealType = mealType.Trim() };
                            foreach (var h in back)
                            {
                                cell.History.Add((h, actual.TryGetValue(key with { Date = h }, out var v) ? v : null));
                            }
                            if (cell.WeeksWithData >= 2)
                            {
                                cell.Average = Math.Round(cell.History.Where(h => h.Actual != null).Average(h => h.Actual!.Value), 2, MidpointRounding.AwayFromZero);
                                cell.Suggested = Math.Ceiling(cell.History.Where(h => h.Actual != null).Average(h => h.Actual!.Value));
                            }
                            cells.Add(cell);
                        }
                    }
                }
            }
            return (cells, customers, locationNames);
        }

        private static string NoteOf(SuggestCell cell, int weeks)
            => $"{SuggestTag}{cell.Suggested!.Value.ToString(CultureInfo.InvariantCulture)}] TB {weeks} tuần cùng thứ: "
                + string.Join(" / ", cell.History.Select(h => (h.Actual is { } a ? DeliveryRules.Num(a) : "–") + " (" + Dm(h.Date) + ")"));

        /// <summary>
        /// FC-1.1 / 1.2 – <c>Forecast/Suggest</c>: tính rồi ghi bản Nháp cho mỗi khóa có gợi ý. Đã có Nháp: bản gợi ý chưa sửa → thay bằng số mới (cùng số → giữ);
        /// bản người đã sửa / nhập tay → bỏ qua trừ khi <c>Overwrite</c>. Không đụng bản Đã công bố. <c>Preview</c> = chỉ tính, không ghi.
        /// </summary>
        public async Task<IResult<ForecastSuggestResultModel>> Suggest(ForecastSuggestRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var (dFrom, dTo) = DefaultSuggestRange();
                DateTime from = (request.From ?? dFrom).Date, to = (request.To ?? (request.From is { } f ? f.Date.AddDays(13) : dTo)).Date;
                int max = Math.Max(1, _rules.ForecastSuggestMaxDays);
                if (to < from || (to - from).TotalDays + 1 > max)
                {
                    return Fail(string.Format(ConstantHelper.Message.ForecastSuggestRange, max));
                }
                var (cells, customers, locations) = await ComputeSuggestionsAsync(from, to, request.CustomerId, null);
                var result = new ForecastSuggestResultModel { From = from, To = to, LookbackWeeks = Math.Max(1, _rules.ForecastLookbackWeeks) };
                if (cells.Count == 0)
                {
                    return Fail(ConstantHelper.Message.ForecastSuggestNoContract);
                }
                var ids = customers.Keys.ToList();
                DateTime end = to.AddDays(1);
                var existing = request.Preview ? [] : _unitOfWork.ForecastRepository.GetAll()
                    .Where(f => !f.IsDeleted && ids.Contains(f.CustomerId) && f.ForecastDate >= from && f.ForecastDate < end).ToList();
                foreach (var cell in cells.OrderBy(c => c.Key.Date).ThenBy(c => customers.GetValueOrDefault(c.Key.CustomerId).Name, StringComparer.CurrentCulture))
                {
                    var model = new ForecastSuggestCellModel
                    {
                        CustomerId = cell.Key.CustomerId, CustomerName = customers.GetValueOrDefault(cell.Key.CustomerId).Name,
                        DeliveryLocationId = cell.Key.LocationId, DeliveryLocationName = cell.Key.LocationId is { } l ? locations.GetValueOrDefault(l) : null,
                        Date = cell.Key.Date, MealType = cell.MealType, Suggested = cell.Suggested, Average = cell.Average, LastWeek = cell.LastWeek,
                        WeeksWithData = cell.WeeksWithData, ListHistory = [.. cell.History.Select(h => new ForecastHistoryPointModel { Date = h.Date, Actual = h.Actual is { } a ? Math.Round(a, 2) : null })],
                    };
                    result.ListCell.Add(model);
                    if (cell.Insufficient)
                    {
                        model.Result = "INSUFFICIENT";
                        result.InsufficientData++;
                        continue;
                    }
                    if (request.Preview)
                    {
                        model.Result = "PREVIEW";
                        continue;
                    }
                    var same = existing.Where(f => f.CustomerId == cell.Key.CustomerId && f.DeliveryLocationId == cell.Key.LocationId && f.ForecastDate.Date == cell.Key.Date
                        && f.ShiftId == null && Meal(f.MealType) == cell.Key.MealType).ToList();
                    var draft = same.Where(f => f.Status == ForecastStatus.Draft).OrderByDescending(f => f.VersionNo).FirstOrDefault();
                    if (draft != null)
                    {
                        bool edited = IsEditedDraft(draft);
                        if (edited && !request.Overwrite)
                        {
                            model.Result = "SKIPPED_EDITED";
                            model.DraftQuantity = draft.ForecastQuantity;
                            result.SkippedEdited++;
                            continue;
                        }
                        if (!edited && draft.ForecastQuantity == cell.Suggested)
                        {
                            model.Result = "UNCHANGED";
                            result.Unchanged++;
                            continue;
                        }
                        model.Result = edited ? "OVERWRITTEN" : "REFRESHED";
                        if (edited)
                        {
                            result.Overwritten++;
                        }
                        else
                        {
                            result.Refreshed++;
                        }
                    }
                    else
                    {
                        model.Result = "CREATED";
                        result.Created++;
                    }
                    foreach (var old in same.Where(f => f.Status == ForecastStatus.Draft))
                    {
                        old.Status = ForecastStatus.Superseded;
                        _unitOfWork.ForecastRepository.Update(old);
                    }
                    var row = new Forecast
                    {
                        Id = Guid.NewGuid(), CustomerId = cell.Key.CustomerId, DeliveryLocationId = cell.Key.LocationId, ForecastDate = cell.Key.Date, MealType = cell.MealType,
                        ForecastQuantity = cell.Suggested!.Value, Quantity = cell.Suggested.Value, VersionNo = (same.Count == 0 ? 0 : same.Max(f => f.VersionNo)) + 1,
                        Status = ForecastStatus.Draft, Note = NoteOf(cell, result.LookbackWeeks),
                    };
                    _unitOfWork.ForecastRepository.Add(row);
                    existing.Add(row);
                }
                if (!request.Preview && result.Created + result.Refreshed + result.Overwritten > 0)
                {
                    await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                }
                _unitOfWork.CommitTransaction();
                string message = request.Preview ? ConstantHelper.Message.GetAllSuccessfully
                    : string.Format(ConstantHelper.Message.ForecastSuggestDone, result.Created + result.Refreshed + result.Overwritten, result.SkippedEdited, result.InsufficientData);
                return Result<ForecastSuggestResultModel>.Success(result, message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Suggest: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ForecastSuggestResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ForecastSuggestResultModel>.Fail(message);
            }
        }

        /// <summary>
        /// FC-1.5 – <c>Forecast/GetAccuracy</c>: ngày đã qua trong [From, To] – so dự báo Đã công bố (bản mới nhất mỗi khóa) với suất thực nhận, theo khách × tuần.
        /// Sai số % = Σ |dự báo − thực nhận| / Σ thực nhận × 100 (WAPE). Chỉ ngày có dự báo hoặc có đơn của khách có dự báo trong kỳ. Tính khi xem, không lưu.
        /// </summary>
        public async Task<IResult<ForecastAccuracyModel>> GetAccuracy(ForecastAccuracyRequest request)
        {
            try
            {
                DateTime today = _clock.Today;
                DateTime to = (request.To ?? today.AddDays(-1)).Date, from = (request.From ?? to.AddDays(-27)).Date;
                if (to >= today)
                {
                    to = today.AddDays(-1);
                }
                if (to < from || (to - from).TotalDays + 1 > _rules.ForecastMaxDays)
                {
                    return Result<ForecastAccuracyModel>.Fail(string.Format(ConstantHelper.Message.ForecastRangeInvalid, _rules.ForecastMaxDays));
                }
                var db = _unitOfWork.DbContext;
                var customerQuery = db.Customer.AsNoTracking().Where(c => !c.IsDeleted);
                if (request.BranchId is { } bid && bid != Guid.Empty)
                {
                    customerQuery = customerQuery.Where(c => c.BranchId == bid);
                }
                if (request.CustomerId is { } cid && cid != Guid.Empty)
                {
                    customerQuery = customerQuery.Where(c => c.Id == cid);
                }
                var names = await customerQuery.ToDictionaryAsync(c => c.Id, c => (c.Code, Name: c.DisplayName ?? c.Name));
                var forecasts = await ForecastRules.LatestPublishedAsync(db, from, to, [.. names.Keys]);
                forecasts = [.. forecasts.Where(f => names.ContainsKey(f.CustomerId))];
                var customerIds = forecasts.Select(f => f.CustomerId).Distinct().ToList();
                var dates = Enumerable.Range(0, (int)(to - from).TotalDays + 1).Select(i => from.AddDays(i)).ToList();
                var actual = customerIds.Count == 0 ? [] : await ActualAsync(db, dates, customerIds);
                var model = new ForecastAccuracyModel { From = from, To = to, WarningPct = _rules.ForecastVariancePctWarning };
                static DateTime WeekOf(DateTime d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
                var points = customerIds.SelectMany(c => dates.Select(d => (Customer: c, Date: d,
                        Forecast: forecasts.Where(f => f.CustomerId == c && f.ForecastDate.Date == d).Sum(f => f.ForecastQuantity > 0 ? f.ForecastQuantity : f.Quantity),
                        HasForecast: forecasts.Any(f => f.CustomerId == c && f.ForecastDate.Date == d),
                        Actual: actual.Where(a => a.Key.CustomerId == c && a.Key.Date == d).Sum(a => a.Value),
                        HasActual: actual.Keys.Any(a => a.CustomerId == c && a.Date == d))))
                    .Where(p => p.HasForecast || p.HasActual).ToList();
                foreach (var g in points.GroupBy(p => (p.Customer, Week: WeekOf(p.Date))).OrderBy(g => names.GetValueOrDefault(g.Key.Customer).Name, StringComparer.CurrentCulture).ThenBy(g => g.Key.Week))
                {
                    model.ListWeek.Add(Row(g.Key.Customer, g.Key.Week, g.Select(p => (p.Forecast, p.Actual)).ToList()));
                }
                foreach (var g in points.GroupBy(p => p.Customer).OrderBy(g => names.GetValueOrDefault(g.Key).Name, StringComparer.CurrentCulture))
                {
                    model.ListCustomer.Add(Row(g.Key, null, g.Select(p => (p.Forecast, p.Actual)).ToList()));
                }
                var all = Row(null, null, points.Select(p => (p.Forecast, p.Actual)).ToList());
                (model.ForecastTotal, model.ActualTotal, model.ErrorPct, model.BiasPct) = (all.Forecast, all.Actual, all.ErrorPct, all.BiasPct);
                model.CustomerCount = model.ListCustomer.Count;
                model.CustomerOverWarning = model.ListCustomer.Count(r => r.ErrorPct is { } e && e > model.WarningPct);
                return Result<ForecastAccuracyModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);

                ForecastAccuracyRowModel Row(Guid? customer, DateTime? week, List<(decimal Forecast, decimal Actual)> p)
                {
                    decimal f = p.Sum(x => x.Forecast), a = p.Sum(x => x.Actual), err = p.Sum(x => Math.Abs(x.Forecast - x.Actual));
                    return new ForecastAccuracyRowModel
                    {
                        CustomerId = customer, CustomerCode = customer is { } c1 ? names.GetValueOrDefault(c1).Code : null, CustomerName = customer is { } c2 ? names.GetValueOrDefault(c2).Name : null,
                        WeekStart = week, Days = p.Count, Forecast = Math.Round(f, 2), Actual = Math.Round(a, 2),
                        ErrorPct = a > 0 ? Math.Round(err / a * 100m, 1, MidpointRounding.AwayFromZero) : null,
                        BiasPct = a > 0 ? Math.Round((f - a) / a * 100m, 1, MidpointRounding.AwayFromZero) : null,
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAccuracy: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>FC-1.3: thông tin gợi ý gắn vào ma trận (khách × ngày) – Σ các khóa của khách trong ngày.</summary>
        private async Task AttachSuggestionAsync(ForecastMatrixModel model, ForecastMatrixRequest request, List<Forecast> drafts)
        {
            var (cells, customers, _) = await ComputeSuggestionsAsync(request.From.Date, request.To.Date, request.CustomerId, request.BranchId);
            if (!string.IsNullOrWhiteSpace(request.MealType))
            {
                string meal = Meal(request.MealType);
                cells = [.. cells.Where(c => c.Key.MealType == meal)];
            }
            foreach (var cid in cells.Select(c => c.Key.CustomerId).Distinct().Where(id => model.Rows.All(r => r.CustomerId != id)))
            {
                model.Rows.Add(new ForecastMatrixRowModel { CustomerId = cid, CustomerCode = customers.GetValueOrDefault(cid).Code, CustomerName = customers.GetValueOrDefault(cid).Name });
            }
            model.Rows = [.. model.Rows.OrderBy(r => r.CustomerName, StringComparer.CurrentCulture)];
            foreach (var row in model.Rows)
            {
                foreach (var g in cells.Where(c => c.Key.CustomerId == row.CustomerId).GroupBy(c => DayKey(c.Key.Date)))
                {
                    var list = g.ToList();
                    var ok = list.Where(c => !c.Insufficient).ToList();
                    int weeks = list.Max(c => c.History.Count);
                    var dayDrafts = drafts.Where(f => f.CustomerId == row.CustomerId && DayKey(f.ForecastDate) == g.Key).ToList();
                    row.Suggest[g.Key] = new ForecastSuggestInfoModel
                    {
                        Suggested = ok.Count == 0 ? null : ok.Sum(c => c.Suggested!.Value),
                        Average = ok.Count == 0 ? null : ok.Sum(c => c.Average!.Value),
                        LastWeek = list.Any(c => c.LastWeek != null) ? Math.Round(list.Sum(c => c.LastWeek ?? 0m), 2) : null,
                        Insufficient = ok.Count == 0,
                        PartlyInsufficient = ok.Count != 0 && ok.Count < list.Count,
                        Edited = dayDrafts.Any(IsEditedDraft),
                        ListHistory = [.. Enumerable.Range(0, weeks).Select(i => new ForecastHistoryPointModel
                        {
                            Date = list.First(c => c.History.Count > i).History[i].Date,
                            Actual = list.Any(c => c.History.Count > i && c.History[i].Actual != null) ? Math.Round(list.Sum(c => c.History.Count > i ? c.History[i].Actual ?? 0m : 0m), 2) : null,
                        })],
                    };
                }
            }
        }
    }
}
