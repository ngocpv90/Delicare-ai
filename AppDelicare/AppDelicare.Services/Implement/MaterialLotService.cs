using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
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
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock
        , Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.BusinessRulesOptions> rules, IInventoryLedgerService inventoryLedgerService
        , ICurrentActor currentActor) : IMaterialLotService
    {
        private readonly AppDelicare.Model.Option.BusinessRulesOptions _rules = rules.Value;
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
                dynamicParameters.Add("@IngredientId", request.IngredientId == Guid.Empty ? null : request.IngredientId);
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

        /// <summary>
        /// BE2.1 (spec 7.5 GET lots, Catalog 118): lô theo nguyên liệu / NCC / trạng thái / QC (LotNumber, QcStatus – bỏ LotNo, QualityStatus) / sắp hết hạn trong N ngày / còn tồn,
        /// kèm tồn (Σ InventoryBalance của lô), số ngày tới hạn, hết hạn, bị chặn xuất (InventoryLotRules: hết hạn, KHONG_DAT / REJECT / HOLD, tạm giữ).
        /// </summary>
        public async Task<IResults<MaterialLotListItemModel>> GetList(MaterialLotListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime today = _clock.Today;
                var query = db.MaterialLot.AsNoTracking().Where(l => !l.IsDeleted);
                if (request.IngredientId is { } ing && ing != Guid.Empty)
                {
                    query = query.Where(l => l.IngredientId == ing);
                }
                if (request.SupplierId is { } sup && sup != Guid.Empty)
                {
                    query = query.Where(l => l.SupplierId == sup);
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    query = query.Where(l => l.Status == request.Status);
                }
                if (!string.IsNullOrWhiteSpace(request.QcStatus))
                {
                    query = query.Where(l => l.QcStatus == request.QcStatus);
                }
                if (request.ExpiringWithinDays is { } days)
                {
                    DateTime limit = today.AddDays(days + 1);
                    query = query.Where(l => l.ExpiryDate != null && l.ExpiryDate < limit);
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string q = request.SearchText.Trim();
                    query = query.Where(l => (l.LotNumber != null && l.LotNumber.Contains(q)) || (l.LotNo != null && l.LotNo.Contains(q)));
                }
                var lots = await query.ToListAsync();
                var lotIds = lots.Select(l => l.Id).ToList();
                var balances = await db.InventoryBalance.AsNoTracking()
                    .Where(b => !b.IsDeleted && ((b.MaterialLotId != null && lotIds.Contains(b.MaterialLotId.Value)) || (b.LotId != null && lotIds.Contains(b.LotId.Value))))
                    .Select(b => new { LotId = b.MaterialLotId ?? b.LotId, b.OnHandQuantity, b.AvailableQuantity }).ToListAsync();
                var ingredientIds = lots.Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var supplierIds = lots.Where(l => l.SupplierId != null).Select(l => l.SupplierId!.Value).Distinct().ToList();
                var suppliers = await db.Supplier.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
                var list = lots.Select(l => new MaterialLotListItemModel
                {
                    Lot = l,
                    IngredientCode = ingredients.GetValueOrDefault(l.IngredientId)?.Code,
                    IngredientName = ingredients.GetValueOrDefault(l.IngredientId)?.Name,
                    SupplierName = l.SupplierId is { } s ? suppliers.GetValueOrDefault(s) : null,
                    OnHandQuantity = balances.Where(b => b.LotId == l.Id).Sum(b => b.OnHandQuantity),
                    AvailableQuantity = balances.Where(b => b.LotId == l.Id).Sum(b => b.AvailableQuantity),
                    DaysToExpiry = l.ExpiryDate is { } e ? (int)(e.Date - today).TotalDays : null,
                    IsExpired = l.ExpiryDate is { } x && x.Date < today,
                    IsBlocked = InventoryLotRules.BlockReason(l, today) != null,
                }).Where(x => request.InStock != true || x.OnHandQuantity > 0)
                .OrderBy(x => x.Lot.ExpiryDate ?? DateTime.MaxValue).ThenBy(x => x.Lot.LotNumber, StringComparer.Ordinal).ToList();
                return Results<MaterialLotListItemModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// BE2.1 (spec 7.5 trace, Catalog 119): truy xuất xuôi của lô – NCC / PO / phiếu nhập / nhiệt độ nhận của lô, rồi phiếu xuất → lệnh SX → mẻ → lệnh đóng gói → đơn → đơn giao → khách
        /// (<see cref="TraceRules.ForwardFromLotAsync"/>). Ngược từ mẻ: ProductionBatch/Trace.
        /// </summary>
        public async Task<IResult<TraceModel>> Trace(Guid id)
        {
            try
            {
                var model = await TraceRules.ForwardFromLotAsync(_unitOfWork.DbContext, id, _rules);
                return model == null ? Result<TraceModel>.Fail(ConstantHelper.Message.LotNotFound) : Result<TraceModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Trace: ", ex.ToString()));
                throw;
            }
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

        /// <summary>
        /// BE2.4 (spec 7.5 LotExpiryJob, Catalog 121 – D-45: không job, gọi bằng thao tác tay / job sau này gọi lại) – xử lý lô hết hạn trong 1 transaction:
        /// lô Khả dụng (hoặc đã Hết hạn nhưng còn tồn) có hạn dùng &lt; hôm nay → <c>Status = Expired</c> + bút toán WASTE toàn bộ tồn từng dòng tồn
        /// (số PH- chung của lần chạy, <c>ReferenceType = LotExpiry</c>, <c>ReferenceId</c> = lô, lý do "Hết hạn sử dụng …"). Lô đang tạm giữ → bỏ qua (giữ để xử lý riêng).
        /// Lô còn ≤ ngày cảnh báo → chỉ liệt kê. <c>Preview</c> = chỉ liệt kê, không ghi. Giữ chỗ nằm trên lô hết hạn được phân bổ lại sang lô khác (sổ cái).
        /// PROPOSED – BA/PO CONFIRM (D-53 d).
        /// </summary>
        public async Task<IResult<LotExpiryResultModel>> ProcessExpired(MaterialLotExpireRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                DateTime today = _clock.Today;
                Guid? warehouseId = request.WarehouseId is { } w && w != Guid.Empty ? w : null;
                var rows = await InventoryLotRules.LoadAsync(db, today, _rules.ExpiringLotDays, warehouseId);
                var result = new LotExpiryResultModel { Preview = request.Preview, Today = today };
                var expiredRows = rows.Where(r => r.MaterialLotId != null && r.IsExpired && r.OnHandQuantity > 0).ToList();
                var lotIds = expiredRows.Select(r => r.MaterialLotId!.Value).Distinct().ToList();
                // Lô hết hạn không còn tồn nhưng vẫn Khả dụng → chỉ đổi trạng thái (không có bút toán) – khi chạy cho mọi kho.
                if (warehouseId == null)
                {
                    lotIds.AddRange(await db.MaterialLot.AsNoTracking()
                        .Where(l => !l.IsDeleted && l.ExpiryDate != null && l.ExpiryDate < today && (l.Status == null || l.Status == MaterialLotStatus.Available) && !lotIds.Contains(l.Id))
                        .Select(l => l.Id).ToListAsync());
                }
                var lots = _unitOfWork.MaterialLotRepository.GetAll().Where(l => lotIds.Contains(l.Id)).ToList();
                var ingredientIds = lots.Select(l => l.IngredientId).Distinct().ToList();
                var ingredientNames = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
                string? number = null;
                foreach (var lot in lots.OrderBy(l => l.ExpiryDate).ThenBy(l => l.LotNumber ?? l.LotNo))
                {
                    var mine = expiredRows.Where(r => r.MaterialLotId == lot.Id).ToList();
                    string lotNo = lot.LotNumber ?? lot.LotNo ?? lot.Id.ToString("N")[..8];
                    var item = new LotExpiryItemModel
                    {
                        MaterialLotId = lot.Id,
                        LotNumber = lotNo,
                        IngredientId = lot.IngredientId,
                        IngredientName = ingredientNames.GetValueOrDefault(lot.IngredientId),
                        ExpiryDate = lot.ExpiryDate,
                        LotStatus = lot.Status,
                        WasteQuantity = mine.Sum(r => r.OnHandQuantity),
                        UnitName = mine.FirstOrDefault()?.UnitName,
                        WasteValue = lot.UnitCost is { } cost ? Math.Round(mine.Sum(r => r.OnHandQuantity) * cost, 2) : null,
                        TransactionCount = mine.Count,
                    };
                    result.ListLot.Add(item);
                    if (string.Equals(lot.Status, MaterialLotStatus.OnHold, StringComparison.OrdinalIgnoreCase))
                    {
                        item.SkippedReason = ConstantHelper.Message.LotExpiryOnHoldSkipped;
                        item.TransactionCount = 0;
                        continue;
                    }
                    if (request.Preview)
                    {
                        continue;
                    }
                    string reason = string.Format(ConstantHelper.Message.LotExpiryReason, lot.ExpiryDate?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var row in mine)
                    {
                        number ??= await _documentNumberService.NextAsync(DocumentKind.Voucher, today);
                        await inventoryLedgerService.PostAsync(new InventoryPostRequest
                        {
                            TransactionType = "WASTE",
                            TransactionNumber = number,
                            TransactionDate = _clock.Now,
                            WarehouseId = row.WarehouseId,
                            StorageLocationId = row.StorageLocationId,
                            IngredientId = row.IngredientId,
                            MaterialLotId = lot.Id,
                            UnitId = row.UnitId,
                            QuantityOut = row.OnHandQuantity,
                            UnitCost = lot.UnitCost,
                            ReferenceType = InventoryReferenceType.LotExpiry,
                            ReferenceId = lot.Id,
                            Reason = reason,
                            Note = string.IsNullOrWhiteSpace(request.Note) ? $"Hủy tồn lô hết hạn {lotNo}" : request.Note.Trim(),
                        });
                    }
                    if (!string.Equals(lot.Status, MaterialLotStatus.Expired, StringComparison.OrdinalIgnoreCase))
                    {
                        lot.Status = MaterialLotStatus.Expired;
                        lot.Note = string.IsNullOrWhiteSpace(lot.Note) ? reason : $"{lot.Note}\n{reason}";
                        _unitOfWork.MaterialLotRepository.Update(lot);
                    }
                }
                result.TransactionNumber = number;
                result.ExpiredLotCount = result.ListLot.Count(l => l.SkippedReason == null);
                result.TotalWasteValue = result.ListLot.Where(l => l.SkippedReason == null).Sum(l => l.WasteValue ?? 0m);
                result.ListExpiring = [.. rows.Where(r => r.MaterialLotId != null && r.IsExpiring && r.BlockReason == null && r.OnHandQuantity > 0)
                    .GroupBy(r => r.MaterialLotId!.Value)
                    .Select(g => new LotExpiryItemModel
                    {
                        MaterialLotId = g.Key,
                        LotNumber = g.First().LotNumber,
                        IngredientId = g.First().IngredientId,
                        IngredientName = g.First().IngredientName,
                        ExpiryDate = g.First().ExpiryDate,
                        LotStatus = g.First().LotStatus,
                        WasteQuantity = g.Sum(r => r.OnHandQuantity),
                        UnitName = g.First().UnitName,
                        WasteValue = g.First().UnitCost is { } c ? Math.Round(g.Sum(r => r.OnHandQuantity) * c, 2) : null,
                    }).OrderBy(i => i.ExpiryDate)];
                if (result.ListLot.Count == 0)
                {
                    result.ListWarning.Add(ConstantHelper.Message.LotExpiryNone);
                }
                if (request.Preview)
                {
                    _unitOfWork.RollbackTransaction();
                }
                else
                {
                    await _unitOfWork.MaterialLotRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    _unitOfWork.CommitTransaction();
                }
                return Result<LotExpiryResultModel>.Success(result, request.Preview ? ConstantHelper.Message.GetAllSuccessfully : ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<LotExpiryResultModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ProcessExpired: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
