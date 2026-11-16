using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProductionOrderService
    {
        public Task<IResults<ProductionOrderViewModel>> GetAll(ProductionOrderSearchRequest request);
        public Task<IResult<bool>> Save(ProductionOrderRequest request, string typeSave);
        public IResults<ProductionOrder> GetAllData();
        public Task<IResult<ProductionOrder>> GetById(Guid id);
        // T17 (Pilot P7): chi tiết lệnh SX (ảnh chụp định lượng, mẻ, phiếu xuất).
        public Task<IResult<ProductionOrderDetailModel>> GetDetail(Guid id);
    }
}
