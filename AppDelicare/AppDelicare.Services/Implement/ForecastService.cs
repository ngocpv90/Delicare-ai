using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Share;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ForecastService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IClock clock, IOptions<BusinessRulesOptions> rules
        , IAuditChangeService auditChangeService) : IForecastService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<ForecastViewModel>> GetAll(ForecastSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CustomerId", request.CustomerId == Guid.Empty ? null : request.CustomerId);
                dynamicParameters.Add("@DeliveryLocationId", request.DeliveryLocationId);
                dynamicParameters.Add("@ForecastDate", request.ForecastDate);
                dynamicParameters.Add("@MealType", request.MealType);
                //dynamicParameters.Add("@ForecastQuantity", request.ForecastQuantity);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ForecastViewModel>("uspForecast_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ForecastViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<Forecast> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ForecastRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Forecast>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(ForecastRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
                    case ConstantHelper.TypeSave.Insert:
                        result = await Insert(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Update:
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>BE2.2: thêm dự báo chỉ qua <c>Forecast/Import</c> (đánh phiên bản, thay bản cũ cùng khóa) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(ForecastRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.ForecastInsertViaImport);

        /// <summary>BE2.2: CRUD Update chỉ sửa số suất / ghi chú của bản Nháp (ghi song song cột cũ Quantity); khóa, phiên bản, trạng thái không đổi qua CRUD.</summary>
        private async Task<string> Update(ForecastRequest request, string typeSave)
        {
            var exist = _unitOfWork.ForecastRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != ForecastStatus.Draft)
            {
                return string.Format(ConstantHelper.Message.ForecastEditOnlyDraft, ForecastStatus.Label(exist.Status));
            }
            if (request.ForecastQuantity < 0)
            {
                return ConstantHelper.Message.ForecastQuantityNegative;
            }
            exist.ForecastQuantity = request.ForecastQuantity;
            exist.Quantity = request.ForecastQuantity;
            exist.Note = request.Note;
            _unitOfWork.ForecastRepository.Update(exist);
            await _unitOfWork.ForecastRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Forecast), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.ForecastRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != ForecastStatus.Draft)
            {
                return string.Format(ConstantHelper.Message.ForecastEditOnlyDraft, ForecastStatus.Label(delete.Status));
            }
            _unitOfWork.ForecastRepository.Update(delete);
            await _unitOfWork.ForecastRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Forecast>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ForecastRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Forecast>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Forecast>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        // ============================================================ BE2.2 – dự báo (spec 6.5, Catalog 87–89)
        // BE2.9: thiếu From / To (= DateTime.MinValue) → báo lỗi thay vì lỗi 500 SqlDateTime overflow.
        private string? RangeError(DateTime from, DateTime to)
            => from == default || to == default || to.Date < from.Date || (to.Date - from.Date).TotalDays + 1 > _rules.ForecastMaxDays
                ? string.Format(ConstantHelper.Message.ForecastRangeInvalid, _rules.ForecastMaxDays) : null;

        private static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Spec 6.5 GET matrix (Catalog 87): khách × ngày trong [From, To], giá trị = Σ ForecastQuantity của bản Đã công bố có VersionNo lớn nhất mỗi khóa
        /// (khách, điểm, ngày, ca, loại suất); DraftValues = Σ bản Nháp mới nhất chưa công bố. Lọc chi nhánh theo <c>Customer.BranchId</c>, khách, loại suất.
        /// </summary>
        public async Task<IResult<ForecastMatrixModel>> GetMatrix(ForecastMatrixRequest request)
        {
            try
            {
                if (RangeError(request.From, request.To) is { } error)
                {
                    return Result<ForecastMatrixModel>.Fail(error);
                }
                var db = _unitOfWork.DbContext;
                DateTime from = request.From.Date, to = request.To.Date, end = to.AddDays(1);
                var customers = db.Customer.AsNoTracking().Where(c => !c.IsDeleted);
                if (request.BranchId is { } bid && bid != Guid.Empty)
                {
                    customers = customers.Where(c => c.BranchId == bid);
                }
                if (request.CustomerId is { } cid && cid != Guid.Empty)
                {
                    customers = customers.Where(c => c.Id == cid);
                }
                var customerIds = await customers.Select(c => c.Id).ToListAsync();
                var rows = await db.Forecast.AsNoTracking()
                    .Where(f => !f.IsDeleted && customerIds.Contains(f.CustomerId) && f.ForecastDate >= from && f.ForecastDate < end
                        && (f.Status == ForecastStatus.Published || f.Status == ForecastStatus.Draft))
                    .ToListAsync();
                if (!string.IsNullOrWhiteSpace(request.MealType))
                {
                    string meal = ForecastRules.Norm(request.MealType);
                    rows = [.. rows.Where(f => ForecastRules.Norm(f.MealType) == meal)];
                }
                var published = rows.Where(f => f.Status == ForecastStatus.Published).GroupBy(ForecastRules.KeyOf)
                    .Select(g => g.OrderByDescending(f => f.VersionNo).First()).ToList();
                var drafts = rows.Where(f => f.Status == ForecastStatus.Draft).GroupBy(ForecastRules.KeyOf)
                    .Select(g => g.OrderByDescending(f => f.VersionNo).First()).ToList();
                var ids = published.Concat(drafts).Select(f => f.CustomerId).Distinct().ToList();
                var names = await db.Customer.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Code, Name = c.DisplayName ?? c.Name });
                var model = new ForecastMatrixModel();
                for (DateTime d = from; d <= to; d = d.AddDays(1))
                {
                    model.Dates.Add(d);
                    model.Totals[DayKey(d)] = 0m;
                }
                foreach (var cid2 in ids.OrderBy(i => names.GetValueOrDefault(i)?.Name, StringComparer.CurrentCulture))
                {
                    var row = new ForecastMatrixRowModel { CustomerId = cid2, CustomerCode = names.GetValueOrDefault(cid2)?.Code, CustomerName = names.GetValueOrDefault(cid2)?.Name };
                    foreach (var g in published.Where(f => f.CustomerId == cid2).GroupBy(f => DayKey(f.ForecastDate)))
                    {
                        row.Values[g.Key] = g.Sum(f => f.ForecastQuantity);
                        model.Totals[g.Key] = model.Totals.GetValueOrDefault(g.Key) + row.Values[g.Key];
                    }
                    foreach (var g in drafts.Where(f => f.CustomerId == cid2).GroupBy(f => DayKey(f.ForecastDate)))
                    {
                        row.DraftValues[g.Key] = g.Sum(f => f.ForecastQuantity);
                    }
                    row.Total = row.Values.Values.Sum();
                    model.Rows.Add(row);
                }
                model.GrandTotal = model.Rows.Sum(r => r.Total);
                return Result<ForecastMatrixModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Spec 6.5 POST bulk (Catalog 88) – 1 transaction: kiểm từng dòng (khách tồn tại, điểm giao thuộc khách, ca tồn tại, số ≥ 0, không trùng khóa trong lô);
        /// mỗi khóa (khách, điểm, ngày, ca, loại suất): bản mới Nháp, VersionNo = lớn nhất của khóa + 1, ghi song song ForecastQuantity / Quantity (cột cũ);
        /// bản Nháp cũ cùng khóa → Bị thay thế ngay; bản Đã công bố cũ giữ hiệu lực tới khi bản mới được công bố (PROPOSED – D-51: spec ghi "bản cũ → Superseded"
        /// ngay khi nhập, em giữ bản đã công bố để mẫu tạo đơn không mất số gợi ý).
        /// </summary>
        public async Task<IResult<ForecastImportResultModel>> Import(ForecastImportRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var items = request.ListItem ?? [];
                if (items.Count == 0)
                {
                    return Fail(ConstantHelper.Message.ForecastNeedsItem);
                }
                var db = _unitOfWork.DbContext;
                var customerIds = items.Select(i => i.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id) && !c.IsDeleted).Select(c => c.Id).ToListAsync();
                var locationIds = items.Where(i => i.DeliveryLocationId != null).Select(i => i.DeliveryLocationId!.Value).Distinct().ToList();
                var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id) && !l.IsDeleted).ToDictionaryAsync(l => l.Id, l => l.CustomerId);
                var shiftIds = items.Where(i => i.ShiftId != null).Select(i => i.ShiftId!.Value).Distinct().ToList();
                var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id) && !s.IsDeleted).Select(s => s.Id).ToListAsync();
                var keys = new Dictionary<ForecastRules.Key, int>();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    string? lineError = !customers.Contains(item.CustomerId) ? ConstantHelper.Message.ContractCustomerNotFound
                        : item.DeliveryLocationId is { } lid && locations.GetValueOrDefault(lid) != item.CustomerId ? ConstantHelper.Message.ForecastLocationMismatch
                        : item.ShiftId is { } sid && !shifts.Contains(sid) ? string.Format(ConstantHelper.Message.NotExist, "Ca").Trim()
                        : item.ForecastQuantity < 0 ? ConstantHelper.Message.ForecastQuantityNegative
                        : null;
                    var key = new ForecastRules.Key(item.CustomerId, item.DeliveryLocationId, item.ForecastDate.Date, item.ShiftId, ForecastRules.Norm(item.MealType));
                    if (lineError == null && keys.TryGetValue(key, out int first))
                    {
                        lineError = string.Format(ConstantHelper.Message.ForecastDuplicateKey, first + 1);
                    }
                    if (lineError != null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.ForecastItemInvalid, i + 1, lineError));
                    }
                    keys[key] = i;
                }
                DateTime minDate = items.Min(i => i.ForecastDate.Date), maxDate = items.Max(i => i.ForecastDate.Date).AddDays(1);
                var existing = _unitOfWork.ForecastRepository.GetAll()
                    .Where(f => !f.IsDeleted && customerIds.Contains(f.CustomerId) && f.ForecastDate >= minDate && f.ForecastDate < maxDate).ToList();
                var result = new ForecastImportResultModel();
                string? note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                foreach (var (key, index) in keys)
                {
                    var item = items[index];
                    var same = existing.Where(f => ForecastRules.KeyOf(f) == key).ToList();
                    int version = (same.Count == 0 ? 0 : same.Max(f => f.VersionNo)) + 1;
                    foreach (var old in same.Where(f => f.Status == ForecastStatus.Draft))
                    {
                        old.Status = ForecastStatus.Superseded;
                        _unitOfWork.ForecastRepository.Update(old);
                        result.Superseded++;
                    }
                    var row = new Forecast
                    {
                        Id = Guid.NewGuid(), CustomerId = item.CustomerId, DeliveryLocationId = item.DeliveryLocationId, ForecastDate = item.ForecastDate.Date,
                        ShiftId = item.ShiftId, MealType = string.IsNullOrWhiteSpace(item.MealType) ? null : item.MealType.Trim(),
                        ForecastQuantity = item.ForecastQuantity, Quantity = item.ForecastQuantity, VersionNo = version, Status = ForecastStatus.Draft, Note = note,
                    };
                    _unitOfWork.ForecastRepository.Add(row);
                    result.ListItem.Add(row);
                    result.Created++;
                    result.VersionNo = Math.Max(result.VersionNo, version);
                }
                await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<ForecastImportResultModel>.Success(result, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Import: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ForecastImportResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ForecastImportResultModel>.Fail(message);
            }
        }

        /// <summary>
        /// Spec 6.5 POST publish (Catalog 89) – 1 transaction: bản Nháp trong [From, To] (lọc khách nếu có) → Đã công bố; mỗi khóa vừa công bố, bản Đã công bố cũ
        /// (VersionNo nhỏ hơn) → Bị thay thế. Nhiều bản Nháp cùng khóa (không xảy ra qua Import) → chỉ công bố bản mới nhất, bản khác Bị thay thế.
        /// </summary>
        public async Task<IResult<ForecastPublishResultModel>> Publish(ForecastPublishRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (RangeError(request.From, request.To) is { } error)
                {
                    return Fail(error);
                }
                DateTime from = request.From.Date, end = request.To.Date.AddDays(1);
                var customerIds = (request.ListCustomerId ?? []).Where(i => i != Guid.Empty).Distinct().ToList();
                var rows = _unitOfWork.ForecastRepository.GetAll()
                    .Where(f => !f.IsDeleted && f.ForecastDate >= from && f.ForecastDate < end
                        && (f.Status == ForecastStatus.Draft || f.Status == ForecastStatus.Published)
                        && (customerIds.Count == 0 || customerIds.Contains(f.CustomerId))).ToList();
                var result = new ForecastPublishResultModel();
                foreach (var group in rows.GroupBy(ForecastRules.KeyOf))
                {
                    var draft = group.Where(f => f.Status == ForecastStatus.Draft).OrderByDescending(f => f.VersionNo).FirstOrDefault();
                    if (draft == null)
                    {
                        continue;
                    }
                    foreach (var other in group.Where(f => f != draft && f.VersionNo <= draft.VersionNo))
                    {
                        other.Status = ForecastStatus.Superseded;
                        _unitOfWork.ForecastRepository.Update(other);
                        result.Superseded++;
                    }
                    draft.Status = ForecastStatus.Published;
                    _unitOfWork.ForecastRepository.Update(draft);
                    result.Published++;
                }
                if (result.Published == 0)
                {
                    return Fail(ConstantHelper.Message.ForecastNothingToPublish);
                }
                await _unitOfWork.ForecastRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ForecastPublishResultModel>.Success(result, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Publish: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ForecastPublishResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ForecastPublishResultModel>.Fail(message);
            }
        }
    }
}
