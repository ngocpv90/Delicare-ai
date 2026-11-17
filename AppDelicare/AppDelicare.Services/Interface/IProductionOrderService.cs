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
        // BE2.4 (Catalog 126, 144, 145, 153): giữ chỗ bổ sung, hủy, đóng lệnh (chênh lệch tiêu hao + giá thành), tính giá thành thực tế.
        public Task<IResult<ProductionOrderDetailModel>> Reserve(AppDelicare.Model.Request.Custom.ProductionActionRequest request);
        public Task<IResult<ProductionOrderDetailModel>> Cancel(AppDelicare.Model.Request.Custom.ProductionOrderCancelRequest request);
        public Task<IResult<ProductionOrderCloseResultModel>> Close(AppDelicare.Model.Request.Custom.ProductionActionRequest request);
        public Task<IResult<ActualCostListModel>> CalculateCost(AppDelicare.Model.Request.Custom.ProductionActionRequest request);
    }
}
