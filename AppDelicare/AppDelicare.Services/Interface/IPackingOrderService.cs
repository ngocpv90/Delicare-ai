using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPackingOrderService
    {
        public Task<IResults<PackingOrderViewModel>> GetAll(PackingOrderSearchRequest request);
        public Task<IResult<bool>> Save(PackingOrderRequest request, string typeSave);
        public IResults<PackingOrder> GetAllData();
        public Task<IResult<PackingOrder>> GetById(Guid id);

        // BE2.1 (spec 10.1) – đóng gói: sinh từ mẻ, lập tay, bắt đầu, kết quả, hủy.
        public Task<IResults<PackingOrderDetailModel>> GetList(PackingOrderListRequest request);
        public Task<IResult<PackingBatchModel>> GetByBatch(Guid id);
        public Task<IResult<PackingBatchModel>> GenerateFromBatch(PackingOrderGenerateRequest request);
        public Task<IResult<PackingBatchModel>> Create(PackingOrderCreateRequest request);
        public Task<IResult<PackingBatchModel>> Start(PackingOrderActionRequest request);
        public Task<IResult<PackingBatchModel>> RecordResult(PackingOrderResultRequest request);
        public Task<IResult<PackingBatchModel>> Cancel(PackingOrderActionRequest request);
        /// <summary>UI-6.34 (D-115): bảng chia suất điểm giao × món.</summary>
        public Task<IResult<AppDelicare.Model.Model.PackingBoardModel>> GetBoard(DateTime? date, Guid? shiftId);
        public Task<IResult<AppDelicare.Model.Model.PackingResultManyModel>> RecordResultMany(AppDelicare.Model.Model.PackingResultManyRequest request);
    }
}
