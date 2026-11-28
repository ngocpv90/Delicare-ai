using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ISupplierMaterialService
    {
        public Task<IResults<SupplierMaterialViewModel>> GetAll(SupplierMaterialSearchRequest request);
        public Task<IResult<bool>> Save(SupplierMaterialRequest request, string typeSave);
        public IResults<SupplierMaterial> GetAllData();
        public Task<IResult<SupplierMaterial>> GetById(Guid id);
        /// <summary>UI-6.19 (D-100): giá mới (tự kết thúc giá cũ), lịch sử, so sánh NCC, ma trận NCC × nguyên liệu, gia hạn hàng loạt, nhập báo giá Excel.</summary>
        public Task<IResult<bool>> SaveNewPrice(AppDelicare.Model.Request.SupplierMaterialRequest request);
        public Task<IResults<AppDelicare.Model.Model.SupplierPriceHistoryModel>> GetPriceHistory(Guid supplierId, Guid ingredientId);
        public Task<IResults<AppDelicare.Model.Model.SupplierPriceCompareModel>> GetCompare(Guid ingredientId, DateTime? date);
        public Task<IResult<AppDelicare.Model.Model.SupplierPriceMatrixModel>> GetMatrix(AppDelicare.Model.Model.SupplierPriceMatrixRequest request);
        public Task<IResult<int>> ExtendMany(AppDelicare.Model.Model.SupplierPriceExtendRequest request);
        public Task<IResult<byte[]>> ExportQuoteTemplate(Guid supplierId);
        public Task<IResult<AppDelicare.Model.Model.SupplierQuoteImportModel>> ImportQuote(Guid supplierId, System.IO.Stream file, bool apply);
        /// <summary>UI-6.51 (D-137 g, GAP-PUR-SPLIT): lưu tỷ lệ chia đơn các NCC của một nguyên liệu – tổng các NCC có tỷ lệ = 100.</summary>
        public Task<IResult<bool>> SaveAllocation(AppDelicare.Model.Request.Custom.SupplierMaterialAllocationRequest request);
    }
}
