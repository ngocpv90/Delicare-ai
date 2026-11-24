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
    public interface IProductionPlanService
    {
        public Task<IResults<ProductionPlanViewModel>> GetAll(ProductionPlanSearchRequest request);
        public Task<IResult<bool>> Save(ProductionPlanRequest request, string typeSave);
        public IResults<ProductionPlan> GetAllData();
        public Task<IResult<ProductionPlan>> GetById(Guid id);
        // T17 (Pilot P7): xác nhận kế hoạch, phát hành → lệnh SX + mẻ ME- (lịch ngược từ giờ xuất bếp), chi tiết, in Phiếu sản xuất.
        public Task<IResult<ProductionPlanDetailModel>> Approve(ProductionActionRequest request);
        public Task<IResult<ProductionPlanDetailModel>> Release(ProductionPlanReleaseRequest request);
        /// <summary>BE2.1 (spec 8.1 generate): sinh / làm lại kế hoạch Nháp của ngày từ đơn Đã xác nhận / Đã chốt (1 KH- / ngày, số phần theo ca).</summary>
        public Task<IResult<ProductionPlanDetailModel>> Generate(ProductionPlanGenerateRequest request);
        /// <summary>BIZ-2.4: sau chốt đơn – tự lập, tự duyệt + phát hành (hoặc giữ Nháp + báo COO).</summary>
        public Task<AppDelicare.Model.Model.ProductionAutoPlanModel> AutoPlanAfterLockAsync(DateTime productionDate);
        /// <summary>D-78 (1): đơn chốt sau khi kế hoạch ngày đã duyệt / phát hành → lệnh + mẻ bổ sung ưu tiên.</summary>
        public Task<AppDelicare.Model.Model.ProductionAutoPlanModel> SupplementForOrderAsync(Guid orderId, string? reason);
        public Task<IResult<ProductionPlanDetailModel>> GetDetail(Guid id);
        public Task<IResult<AppDelicare.Model.Model.ProductionPlanDaysModel>> GetDays(ProductionPlanDaysRequest request);
        public Task<IResult<AppDelicare.Model.Model.ProductionPlanRecalculateModel>> Recalculate(ProductionPlanRecalculateRequest request);
        public Task<IResult<AppDelicare.Model.Model.ProductionPlanShortageModel>> GetShortage(Guid id);
        public Task<IResult<AppDelicare.Model.Model.ProductionDayBoardModel>> GetDayBoard(Guid id);
        /// <summary>UI-6 (D-80): trang chi tiết toàn màn (…/GetOverview).</summary>
        public Task<IResult<ProductionPlanOverviewModel>> GetOverview(Guid id);
        public Task<IResult<byte[]>> ExportProductionSheet(Guid id);
    }
}
