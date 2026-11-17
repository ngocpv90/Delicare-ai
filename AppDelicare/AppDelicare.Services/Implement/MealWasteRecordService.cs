#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
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
    /// <summary>T17: hao hụt gắn mẻ qua ProductionBatch/RecordWaste. BE2.1 (spec 8.4): ghi hao hụt chung (Record – mẻ / lệnh SX / món + ca), danh sách theo kỳ.</summary>
    public class MealWasteRecordService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor, IOptions<ReferenceListsOptions> referenceLists) : IMealWasteRecordService
    {
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<MealWasteRecordViewModel>> GetAll(MealWasteRecordSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionBatchId", request.ProductionBatchId);
                dynamicParameters.Add("@ProductionOrderId", request.ProductionOrderId);
                dynamicParameters.Add("@DishId", request.DishId);
                dynamicParameters.Add("@ShiftId", request.ShiftId);
                dynamicParameters.Add("@WasteType", request.WasteType);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MealWasteRecordViewModel>("uspMealWasteRecord_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<MealWasteRecordViewModel>
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

        public IResults<MealWasteRecord> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MealWasteRecordRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MealWasteRecord>
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

        public async Task<IResult<bool>> Save(MealWasteRecordRequest request, string typeSave)
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
          break;                    default:
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

        private async Task<string> Insert(MealWasteRecordRequest request, string typeSave)
        {
            // T17: hao hụt gắn mẻ ghi qua ProductionBatch/RecordWaste (cập nhật WasteQty của mẻ, người ghi, giờ).
            if (request.ProductionBatchId is { } batchId && batchId != Guid.Empty)
            {
                return ConstantHelper.Message.WasteBatchViaAction;
            }
            // BE2.1: ghi mới qua MealWasteRecord/Record (người ghi, giờ, loại theo danh mục, cập nhật WasteQty của mẻ) – CRUD Insert không ghi.
            return await Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Hao hụt", "MealWasteRecord/Record"));
        }

        private async Task<string> Update(MealWasteRecordRequest request, string typeSave)
        {
            var exist = _unitOfWork.MealWasteRecordRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T17: dòng hao hụt của mẻ không sửa qua CRUD (WasteQty của mẻ tính từ các dòng này).
            if (exist.ProductionBatchId != null || request.ProductionBatchId is { } batchId && batchId != Guid.Empty)
            {
                return ConstantHelper.Message.WasteBatchViaAction;
            }
            var entity = _mapper.Map(request, exist);
            _unitOfWork.MealWasteRecordRepository.Update(entity);
            await _unitOfWork.MealWasteRecordRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: bản ghi đang được tham chiếu (quan hệ logic, truy vấn có tham số) → không cho xóa.
            if ((await _referenceGuard.FindUsagesAsync("MealWasteRecord", (Guid)id)).Count > 0)
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MealWasteRecordRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.ProductionBatchId != null)
            {
                return ConstantHelper.Message.WasteBatchViaAction;
            }
            _unitOfWork.MealWasteRecordRepository.Update(delete);
            await _unitOfWork.MealWasteRecordRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>
        /// BE2.1 (spec 8.4 POST waste-records, Catalog 156): ghi hao hụt / suất thừa / hư hỏng – 1 transaction. Gắn mẻ (→ lệnh SX, món, ca của mẻ; WasteQty của mẻ = Σ suất hao hụt,
        /// mẻ phải đã nấu: Đang nấu / Chờ QC / Đạt / Đã đóng gói / Không đạt), hoặc lệnh SX (→ món, ca), hoặc món + ca. Loại hao hụt theo ReferenceLists WasteType;
        /// số suất hoặc số kg &gt; 0; OccurredAt = giờ gửi (không sau hiện tại) / bây giờ; RecordedBy = người thao tác.
        /// </summary>
        public async Task<IResult<MealWasteItemModel>> Record(MealWasteRecordCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                if (request.QuantityMeals is not > 0 && request.QuantityKg is not > 0 || request.QuantityMeals < 0 || request.QuantityKg < 0)
                {
                    return Fail(ConstantHelper.Message.WasteQuantityRequired);
                }
                string type = (request.WasteType ?? string.Empty).Trim().ToUpperInvariant();
                if (_referenceLists.Lists.TryGetValue("WasteType", out var types) && types.Count != 0 && !types.Any(t => string.Equals(t.Code, type, StringComparison.OrdinalIgnoreCase)))
                {
                    return Fail(string.Format(ConstantHelper.Message.WasteTypeInvalid, type));
                }
                Guid? batchId = request.ProductionBatchId is { } b && b != Guid.Empty ? b : null;
                Guid? orderId = request.ProductionOrderId is { } o && o != Guid.Empty ? o : null;
                Guid? dishId = request.DishId is { } d && d != Guid.Empty ? d : null;
                Guid? shiftId = request.ShiftId is { } s && s != Guid.Empty ? s : null;
                ProductionBatch? batch = null;
                if (batchId != null)
                {
                    batch = _unitOfWork.ProductionBatchRepository.GetSingle(x => x.Id == batchId && !x.IsDeleted);
                    if (batch == null)
                    {
                        return Fail(ConstantHelper.Message.BatchNotFound);
                    }
                    if (batch.Status == ProductionBatchStatus.Planned)
                    {
                        return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, "ghi hao hụt", "mẻ " + (batch.BatchNumber ?? batch.BatchNo),
                            "đã bắt đầu nấu", ProductionBatchStatus.Label(batch.Status)));
                    }
                    orderId = batch.ProductionOrderId;
                    dishId = batch.DishId;
                }
                if (orderId != null)
                {
                    var productionOrder = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted);
                    if (productionOrder == null)
                    {
                        return Fail(ConstantHelper.Message.ProductionOrderNotFound);
                    }
                    dishId ??= productionOrder.DishId;
                    shiftId ??= productionOrder.ShiftId;
                }
                if (dishId == null)
                {
                    return Fail(ConstantHelper.Message.WasteTargetRequired);
                }
                if (!await db.Dish.AsNoTracking().AnyAsync(x => x.Id == dishId && !x.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.RecordNotFoundMessage + " (món)");
                }
                if (shiftId != null && !await db.Shift.AsNoTracking().AnyAsync(x => x.Id == shiftId && !x.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.ShiftNotFound);
                }
                DateTime now = _clock.Now;
                var record = new MealWasteRecord
                {
                    Id = Guid.NewGuid(),
                    ProductionBatchId = batchId,
                    ProductionOrderId = orderId,
                    DishId = dishId,
                    ShiftId = shiftId,
                    WasteType = type,
                    QuantityMeals = request.QuantityMeals,
                    QuantityKg = request.QuantityKg,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    Disposition = string.IsNullOrWhiteSpace(request.Disposition) ? null : request.Disposition.Trim(),
                    OccurredAt = request.OccurredAt is { } at && at <= now ? at : now,
                    RecordedBy = actor.UserId,
                };
                _unitOfWork.MealWasteRecordRepository.Add(record);
                if (batch != null)
                {
                    decimal sum = await db.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.ProductionBatchId == batch.Id).SumAsync(w => w.QuantityMeals ?? 0m);
                    batch.WasteQty = sum + (request.QuantityMeals ?? 0m);
                    _unitOfWork.ProductionBatchRepository.Update(batch);
                }
                await _unitOfWork.MealWasteRecordRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<MealWasteItemModel>.Success((await ItemsAsync([record])).Single(), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Record: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<MealWasteItemModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<MealWasteItemModel>.Fail(message);
            }
        }

        private async Task<List<MealWasteItemModel>> ItemsAsync(List<MealWasteRecord> list)
        {
            var db = _unitOfWork.DbContext;
            var dishIds = list.Where(x => x.DishId != null).Select(x => x.DishId!.Value).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(x => dishIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
            var shiftIds = list.Where(x => x.ShiftId != null).Select(x => x.ShiftId!.Value).Distinct().ToList();
            var shifts = await db.Shift.AsNoTracking().Where(x => shiftIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code);
            var batchIds = list.Where(x => x.ProductionBatchId != null).Select(x => x.ProductionBatchId!.Value).Distinct().ToList();
            var batches = await db.ProductionBatch.AsNoTracking().Where(x => batchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.BatchNumber ?? x.BatchNo);
            var orderIds = list.Where(x => x.ProductionOrderId != null).Select(x => x.ProductionOrderId!.Value).Distinct().ToList();
            var orders = await db.ProductionOrder.AsNoTracking().Where(x => orderIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ProductionOrderNumber);
            var userIds = list.Where(x => x.RecordedBy != null).Select(x => x.RecordedBy!.Value).Distinct().ToList();
            var users = await db.User.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName);
            var labels = _referenceLists.Lists.TryGetValue("WasteType", out var types) ? types.ToDictionary(t => t.Code, t => t.Label, StringComparer.OrdinalIgnoreCase) : [];
            return [.. list.Select(x => new MealWasteItemModel
            {
                Record = x,
                WasteTypeName = x.WasteType != null ? labels.GetValueOrDefault(x.WasteType, x.WasteType) : null,
                DishName = x.DishId is { } d ? dishes.GetValueOrDefault(d) : null,
                ShiftCode = x.ShiftId is { } s ? shifts.GetValueOrDefault(s) : null,
                BatchNumber = x.ProductionBatchId is { } b ? batches.GetValueOrDefault(b) : null,
                ProductionOrderNumber = x.ProductionOrderId is { } o ? orders.GetValueOrDefault(o) : null,
                RecordedByName = x.RecordedBy is { } u ? users.GetValueOrDefault(u) : null,
            })];
        }

        /// <summary>BE2.1: danh sách hao hụt theo kỳ (OccurredAt) / món / ca / mẻ / loại + tổng suất / kg và theo loại.</summary>
        public async Task<IResult<MealWasteListModel>> GetList(MealWasteListRequest request)
        {
            try
            {
                var query = _unitOfWork.DbContext.MealWasteRecord.AsNoTracking().Where(x => !x.IsDeleted);
                if (request.From is { } from)
                {
                    query = query.Where(x => x.OccurredAt >= from.Date);
                }
                if (request.To is { } to)
                {
                    DateTime end = to.Date.AddDays(1);
                    query = query.Where(x => x.OccurredAt < end);
                }
                if (request.DishId is { } dish && dish != Guid.Empty)
                {
                    query = query.Where(x => x.DishId == dish);
                }
                if (request.ShiftId is { } shift && shift != Guid.Empty)
                {
                    query = query.Where(x => x.ShiftId == shift);
                }
                if (request.ProductionBatchId is { } batch && batch != Guid.Empty)
                {
                    query = query.Where(x => x.ProductionBatchId == batch);
                }
                if (!string.IsNullOrWhiteSpace(request.WasteType))
                {
                    query = query.Where(x => x.WasteType == request.WasteType.Trim().ToUpper());
                }
                var list = await query.OrderByDescending(x => x.OccurredAt).ToListAsync();
                return Result<MealWasteListModel>.Success(new MealWasteListModel
                {
                    ListItem = await ItemsAsync(list),
                    TotalMeals = list.Sum(x => x.QuantityMeals ?? 0m),
                    TotalKg = list.Sum(x => x.QuantityKg ?? 0m),
                    MealsByType = list.GroupBy(x => x.WasteType ?? string.Empty).ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityMeals ?? 0m)),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<MealWasteRecord>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MealWasteRecordRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MealWasteRecord>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MealWasteRecord>
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
    }
}
