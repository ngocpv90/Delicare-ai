using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using AutoMapper;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>
    /// RETAIL-1: sản phẩm bán lẻ – mẫu WarehouseService. Mã G1 <c>SPL-NNNNN</c> máy chủ sinh khi thêm, không đổi khi sửa.
    /// Nhóm theo ReferenceLists:RetailCategory, bữa bán theo ReferenceLists:MealType (lưu nhãn nối ';'). Xóa: đã có trong đơn → không xóa, chỉ Ngừng bán.
    /// </summary>
    public class RetailProductService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IDocumentNumberService documentNumbers,
        IOptions<ReferenceListsOptions> referenceLists) : IRetailProductService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ReferenceListsOptions _lists = referenceLists.Value;

        public async Task<IResults<RetailProductViewModel>> GetAll(RetailProductSearchRequest request)
        {
            try
            {
                DynamicParameters p = new();
                p.Add("@SearchText", request.SearchText);
                p.Add("@CategoryCode", request.CategoryCode);
                p.Add("@Status", request.Status);
                p.Add(ConstantHelper.Page, request.Page);
                p.Add(ConstantHelper.PageSize, request.PageSize);
                p.Add(ConstantHelper.SortField, request.SortField);
                p.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await dapperRepository.QueryMultipleUsingStoreProcAsync<RetailProductViewModel>("uspRetailProduct_GetAll", p)).ToList();
                int i = 0;
                list.ForEach(x => x.STT = (request.Page - 1) * request.PageSize + ++i);
                return new Results<RetailProductViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<RetailProduct> GetAllData()
        {
            try
            {
                var list = _unitOfWork.RetailProductRepository.GetAll().Where(x => !x.IsDeleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
                return new Results<RetailProduct>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(RetailProductRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                string result = typeSave switch
                {
                    ConstantHelper.TypeSave.Insert => await Insert(request, typeSave),
                    ConstantHelper.TypeSave.Update => await Update(request, typeSave),
                    ConstantHelper.TypeSave.Delete => await Delete(request.Id, typeSave),
                    _ => "Loại thao tác lưu dữ liệu không hợp lệ.",
                };
                if (!string.IsNullOrEmpty(result))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(result, false);
                }
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<string> Insert(RetailProductRequest request, string typeSave)
        {
            string? error = Normalize(request);
            if (error != null)
            {
                return error;
            }
            var insert = mapper.Map<RetailProduct>(request);
            insert.Id = Guid.NewGuid();
            insert.Code = await documentNumbers.NextCodeAsync(DocumentKind.RetailProduct);
            _unitOfWork.RetailProductRepository.Add(insert);
            await _unitOfWork.RetailProductRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(RetailProductRequest request, string typeSave)
        {
            var exist = request.Id == null ? null : _unitOfWork.RetailProductRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string? error = Normalize(request);
            if (error != null)
            {
                return error;
            }
            string? code = exist.Code;   // G1: mã không đổi
            var entity = mapper.Map(request, exist);
            entity.Code = code;
            _unitOfWork.RetailProductRepository.Update(entity);
            await _unitOfWork.RetailProductRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            var delete = id == null ? null : _unitOfWork.RetailProductRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // Quan hệ RetailOrderLine.RetailProductId chưa có trong LOGICAL_RELATIONS (bảng mới) → kiểm trực tiếp.
            if (await _unitOfWork.DbContext.RetailOrderLine.AsNoTracking().AnyAsync(l => l.RetailProductId == delete.Id))
            {
                return RetailMessage.ProductInUse;
            }
            _unitOfWork.RetailProductRepository.Update(delete);
            await _unitOfWork.RetailProductRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>Kiểm + chuẩn hóa dữ liệu nhập (tên, nhóm, đơn vị, giá, bữa bán, trạng thái). Trả lỗi hoặc null.</summary>
        private string? Normalize(RetailProductRequest r)
        {
            r.Name = RetailRules.Clean(r.Name, 255);
            if (r.Name == null || r.Name.Length < 2)
            {
                return RetailMessage.ProductNameRequired;
            }
            var cats = RetailRules.Categories(_lists);
            var cat = cats.FirstOrDefault(c => string.Equals(c.Code, r.CategoryCode?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cat == null && (cats.Count > 0 || string.IsNullOrWhiteSpace(r.CategoryCode)))
            {
                return RetailMessage.ProductCategoryInvalid;
            }
            r.CategoryCode = cat?.Code ?? r.CategoryCode!.Trim();
            r.Unit = RetailRules.Clean(r.Unit, 50);
            if (r.Unit == null)
            {
                return RetailMessage.ProductUnitRequired;
            }
            if (r.Price is not > 0)
            {
                return RetailMessage.ProductPriceInvalid;
            }
            var meals = (r.MealTypes ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => RetailRules.ResolveMeal(m, _lists)).ToList();
            if (meals.Count == 0 || meals.Any(m => m == null))
            {
                return RetailMessage.ProductMealsRequired;
            }
            r.MealTypes = string.Join(";", meals.Distinct());
            r.Status = string.IsNullOrWhiteSpace(r.Status) ? RetailCodes.ProductActive
                : RetailCodes.ProductStatuses.FirstOrDefault(s => string.Equals(s, r.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (r.Status == null)
            {
                return RetailMessage.ProductStatusInvalid;
            }
            r.SortOrder = Math.Max(0, r.SortOrder ?? 0);
            r.Description = RetailRules.Clean(r.Description, 1000);
            r.Tags = string.Join(";", RetailRules.Tags(r.Tags));
            r.Icon = RetailRules.Clean(r.Icon, 50);
            r.ImageUrl = RetailRules.Clean(r.ImageUrl, 500);
            return null;
        }

        public async Task<IResult<RetailProduct>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.RetailProductRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                return data == null ? Result<RetailProduct>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<RetailProduct>.Success(data, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }
    }
}
