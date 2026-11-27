// RETAIL-1: sản phẩm bán lẻ (#/sal-retail-product) – CRUD theo mẫu Warehouse; mã G1 SPL-NNNNN do máy chủ sinh.
#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IRetailProductService
    {
        public Task<IResults<RetailProductViewModel>> GetAll(RetailProductSearchRequest request);
        public Task<IResult<bool>> Save(RetailProductRequest request, string typeSave);
        public IResults<RetailProduct> GetAllData();
        public Task<IResult<RetailProduct>> GetById(Guid id);
    }
}
