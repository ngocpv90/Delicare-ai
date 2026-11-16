using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;
using AppDelicare.Data.Share;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    public class MaterialLotService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock) : IMaterialLotService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;

        public async Task<IResults<MaterialLotViewModel>> GetAll(MaterialLotSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                dynamicParameters.Add("@SupplierId", request.SupplierId);
                dynamicParameters.Add("@GoodsReceiptLineId", request.GoodsReceiptLineId);
                dynamicParameters.Add("@LotNumber", request.LotNumber);
                dynamicParameters.Add("@ManufactureDate", request.ManufactureDate);
                dynamicParameters.Add("@ExpiryDate", request.ExpiryDate);
                dynamicParameters.Add("@ReceivedDate", request.ReceivedDate);
                dynamicParameters.Add("@QualityStatus", request.QualityStatus);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MaterialLotViewModel>("uspMaterialLot_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MaterialLotViewModel>
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

        public IResults<MaterialLot> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MaterialLotRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MaterialLot>
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

        public async Task<IResult<bool>> Save(MaterialLotRequest request, string typeSave)
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

        /// <summary>
        /// T16: thêm lô tay (tồn đầu kỳ / lô chưa qua luồng nhập T15 – PROPOSED). Số lô bỏ trống + có NCC → sinh <c>NL-YYMMDD-&lt;mã NCC&gt;-NN</c> (T11).
        /// Trạng thái chỉ Available / OnHold (mặc định Available). Ghi song song cột cũ LotNo / QualityStatus (DEPRECATED_COLUMNS).
        /// </summary>
        private async Task<string> Insert(MaterialLotRequest request, string typeSave)
        {
            var insert = _mapper.Map<MaterialLot>(request);
            if (!await _unitOfWork.DbContext.Ingredient.AsNoTracking().AnyAsync(i => i.Id == request.IngredientId && !i.IsDeleted))
            {
                return ConstantHelper.Message.IngredientNotFound;
            }
            string? number = request.LotNumber?.Trim();
            if (string.IsNullOrEmpty(number))
            {
                string? supplierCode = request.SupplierId == null ? null
                    : await _unitOfWork.DbContext.Supplier.AsNoTracking().Where(s => s.Id == request.SupplierId).Select(s => s.Code).FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(supplierCode))
                {
                    return ConstantHelper.Message.LotNumberRequired;
                }
                number = await _documentNumberService.NextAsync(DocumentKind.MaterialLot, (request.ReceivedDate ?? _clock.Today).Date, supplierCode);
            }
            insert.LotNumber = insert.LotNo = number;
            insert.QcStatus = insert.QualityStatus = string.IsNullOrWhiteSpace(request.QcStatus) ? request.QualityStatus : request.QcStatus;
            insert.Status = string.Equals(request.Status, MaterialLotStatus.OnHold, StringComparison.OrdinalIgnoreCase) ? MaterialLotStatus.OnHold : MaterialLotStatus.Available;
            insert.ReceivedDate ??= _clock.Today;
            _unitOfWork.MaterialLotRepository.Add(insert);
            await _unitOfWork.MaterialLotRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>
        /// T16: sửa thông tin lô. Không đổi trạng thái (dùng Hold / Release); không đổi nguyên liệu khi lô đã có giao dịch kho;
        /// lô QC KHONG_DAT / REJECT không được sửa thành đạt (chỉ qua luồng QC T15 – PROPOSED). Ghi song song LotNo / QualityStatus.
        /// </summary>
        private async Task<string> Update(MaterialLotRequest request, string typeSave)
        {
            var exist = _unitOfWork.MaterialLotRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string? status = exist.Status;
            string? oldQc = exist.QcStatus ?? exist.QualityStatus;
            Guid oldIngredient = exist.IngredientId;
            string? oldNumber = exist.LotNumber ?? exist.LotNo;
            string? newQc = string.IsNullOrWhiteSpace(request.QcStatus) ? request.QualityStatus : request.QcStatus;
            if (!string.IsNullOrWhiteSpace(oldQc) && LotQcStatus.Failed.Contains(oldQc.Trim())
                && !string.Equals(oldQc.Trim(), newQc?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(ConstantHelper.Message.LotQcFailedLocked, oldQc);
            }
            if (request.IngredientId != oldIngredient
                && await _unitOfWork.DbContext.InventoryTransaction.AsNoTracking().AnyAsync(t => t.MaterialLotId == exist.Id && !t.IsDeleted))
            {
                return ConstantHelper.Message.LotIngredientLocked;
            }
            var update = _mapper.Map(request, exist);
            update.Status = status;
            update.LotNumber = update.LotNo = string.IsNullOrWhiteSpace(request.LotNumber) ? oldNumber : request.LotNumber.Trim();
            update.QcStatus = update.QualityStatus = newQc;
            _unitOfWork.MaterialLotRepository.Update(update);
            await _unitOfWork.MaterialLotRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T16: lô đã có giao dịch kho / tồn thì không xóa (sổ chỉ ghi thêm).
            if (await _unitOfWork.DbContext.InventoryTransaction.AsNoTracking().AnyAsync(t => t.MaterialLotId == id && !t.IsDeleted)
                || await _unitOfWork.DbContext.InventoryBalance.AsNoTracking().AnyAsync(b => b.MaterialLotId == id && !b.IsDeleted))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(MaterialLot), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MaterialLotRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.MaterialLotRepository.Update(delete);
            await _unitOfWork.MaterialLotRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MaterialLot>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MaterialLotRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MaterialLot>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MaterialLot>
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

        /// <summary>
        /// T16 (spec 7.5 hold): tạm giữ lô Available → OnHold, lý do bắt buộc (nối vào Note). Lô tạm giữ bị chặn xuất / không được gợi ý FEFO.
        /// Không sửa InventoryBalance (tồn chỉ tính từ sổ) – "khả dụng xuất được" của lô tính = 0 khi đọc (PROPOSED).
        /// </summary>
        public Task<IResult<MaterialLot>> Hold(MaterialLotHoldRequest request)
        {
            string reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length is < 3 or > 1000)
            {
                return Task.FromResult<IResult<MaterialLot>>(Result<MaterialLot>.Fail(ConstantHelper.Message.ReasonRequired));
            }
            return ChangeStatus(request.Id, MaterialLotStatus.Available, MaterialLotStatus.OnHold, "Tạm giữ: " + reason, ConstantHelper.Message.LotHoldInvalid);
        }

        /// <summary>T16 (spec 7.5 release): mở lô OnHold → Available (lý do tùy chọn). Lô hết hạn / QC không đạt vẫn bị chặn xuất theo quy tắc riêng.</summary>
        public Task<IResult<MaterialLot>> Release(MaterialLotHoldRequest request)
        {
            string? reason = request.Reason?.Trim();
            return ChangeStatus(request.Id, MaterialLotStatus.OnHold, MaterialLotStatus.Available,
                string.IsNullOrEmpty(reason) ? "Mở tạm giữ" : "Mở tạm giữ: " + reason, ConstantHelper.Message.LotReleaseInvalid);
        }

        private async Task<IResult<MaterialLot>> ChangeStatus(Guid id, string from, string to, string note, string invalidMessage)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var lot = _unitOfWork.MaterialLotRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                // Lô cũ chưa có trạng thái coi như Available.
                string current = string.IsNullOrWhiteSpace(lot?.Status) ? MaterialLotStatus.Available : lot!.Status!;
                string? error = lot == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : !string.Equals(current, from, StringComparison.OrdinalIgnoreCase) ? string.Format(invalidMessage, MaterialLotStatus.Label(current))
                    : null;
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<MaterialLot>.Fail(error);
                }
                lot!.Status = to;
                lot.Note = string.IsNullOrWhiteSpace(lot.Note) ? note : $"{lot.Note}\n{note}";
                _unitOfWork.MaterialLotRepository.Update(lot);
                await _unitOfWork.MaterialLotRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<MaterialLot>.Success(lot, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ChangeStatus: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
