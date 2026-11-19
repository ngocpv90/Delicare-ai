using AppDelicare.Data.Share;
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
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Implement
{
    public class SupplierService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, IDocumentNumberService documentNumberService) : ISupplierService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<SupplierViewModel>> GetAll(SupplierSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId == Guid.Empty ? null : request.CompanyId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<SupplierViewModel>("uspSupplier_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<SupplierViewModel>
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

        public IResults<Supplier> GetAllData()
        {
            try
            {
                var list = _unitOfWork.SupplierRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Supplier>
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

        public async Task<IResult<bool>> Save(SupplierRequest request, string typeSave)
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
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nhà cung cấp", _createdCode)
                        : ConstantHelper.Message.SaveSuccess,
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

        private async Task<string> Insert(SupplierRequest request, string typeSave)
        {
            var insert = _mapper.Map<Supplier>(request);
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Supplier);
            _createdCode = insert.Code;
            _unitOfWork.SupplierRepository.Add(insert);
            await _unitOfWork.SupplierRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(SupplierRequest request, string typeSave)
        {
            var exist = _unitOfWork.SupplierRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            var update = _mapper.Map(request, exist);
            _unitOfWork.SupplierRepository.Update(update);
            await _unitOfWork.SupplierRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Supplier), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.SupplierRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.SupplierRepository.Update(delete);
            await _unitOfWork.SupplierRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Supplier>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.SupplierRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Supplier>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Supplier>
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

        /// <summary>PO đã đóng (không tính "đang mở").</summary>
        private static readonly string[] ClosedPoStatuses = ["Received", "Closed", "Cancelled"];

        /// <summary>T45R (spec 4.3): tổng quan NCC – bảng giá, PO đang mở, 20 sự cố gần nhất, tỷ lệ đúng hạn / trả hàng 90 ngày.</summary>
        public async Task<IResult<SupplierOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var supplier = await _unitOfWork.SupplierRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (supplier == null)
                {
                    return Result<SupplierOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                DateTime since = clock.Today /* Rà soát BE: giờ nghiệp vụ VN */.AddDays(-90);
                var lines = await (from l in db.GoodsReceiptLine.AsNoTracking()
                                   join r in db.GoodsReceipt.AsNoTracking() on l.GoodsReceiptId equals r.Id
                                   where !l.IsDeleted && !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since
                                   select new { l.ReceivedQuantity, l.RejectedQuantity }).ToListAsync();
                decimal received = lines.Sum(l => l.ReceivedQuantity);
                var receipts = await (from r in db.GoodsReceipt.AsNoTracking()
                                      join p in db.PurchaseOrder.AsNoTracking() on r.PurchaseOrderId equals p.Id
                                      where !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since
                                      select new { r.ReceiptDate, Expected = p.ExpectedDeliveryDate ?? p.ExpectedDate }).ToListAsync();
                var withExpected = receipts.Where(r => r.Expected != null).ToList();
                var model = new SupplierOverviewModel
                {
                    Supplier = supplier,
                    ListMaterial = await (from m in db.SupplierMaterial.AsNoTracking()
                                          join i in db.Ingredient.AsNoTracking() on m.IngredientId equals i.Id into ig
                                          from i in ig.DefaultIfEmpty()
                                          where !m.IsDeleted && m.SupplierId == id
                                          orderby i.Name
                                          select new SupplierMaterialBriefModel
                                          {
                                              Id = m.Id, IngredientId = m.IngredientId, IngredientName = i.Name, UnitPrice = m.UnitPrice, IsPreferred = m.IsPreferred,
                                              EffectiveFrom = m.EffectiveFrom, EffectiveTo = m.EffectiveTo, Status = m.Status,
                                          }).ToListAsync(),
                    ListOpenPurchaseOrder = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.SupplierId == id && !ClosedPoStatuses.Contains(p.Status!))
                        .OrderBy(p => p.ExpectedDeliveryDate).ToListAsync(),
                    ListIssue = await db.SupplierIssue.AsNoTracking().Where(x => !x.IsDeleted && x.SupplierId == id).OrderByDescending(x => x.ReportedAt).Take(20).ToListAsync(),
                    RejectRate = received > 0 ? Math.Round(lines.Sum(l => l.RejectedQuantity) / received * 100, 2) : null,
                    OnTimeRate = withExpected.Count > 0 ? Math.Round(withExpected.Count(r => r.ReceiptDate.Date <= r.Expected!.Value.Date) * 100m / withExpected.Count, 2) : null,
                };
                return Result<SupplierOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }
    }
}
