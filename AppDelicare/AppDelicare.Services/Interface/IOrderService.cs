using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrderService
    {
        public Task<IResults<OrderViewModel>> GetAll(OrderSearchRequest request);
        public Task<IResult<bool>> Save(OrderRequest request, string typeSave);
        public IResults<Order> GetAllData();
        public Task<IResult<Order>> GetById(Guid id);
        // <summary>T13 (spec 6.6): khung tạo đơn, tạo, sửa số suất, máy trạng thái, giờ chốt 20h D-1, lệch suất > 30%.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderTemplateModel>> GetNewTemplate(Guid customerId, DateTime mealDate);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> GetDetail(Guid id);
        /// <summary>UI-6 (6.2 nhóm A): trang chi tiết toàn màn – chi tiết + yêu cầu duyệt mới nhất + giao hàng / đối soát / phản ánh.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderPageModel>> GetOverview(Guid id);
        /// <summary>BE2.1 (spec 6.6 view=board): bảng đơn 8 trạng thái + statusCounts.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderBoardModel>> GetBoard(AppDelicare.Model.Request.Custom.OrderBoardRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Create(AppDelicare.Model.Request.Custom.OrderCreateRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> UpdateLines(AppDelicare.Model.Request.Custom.OrderUpdateLinesRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Submit(AppDelicare.Model.Request.Custom.OrderActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Confirm(AppDelicare.Model.Request.Custom.OrderActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Lock(AppDelicare.Model.Request.Custom.OrderActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> Cancel(AppDelicare.Model.Request.Custom.OrderCancelRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderLockSummaryModel>> LockByMealDate(AppDelicare.Model.Request.Custom.OrderLockByMealDateRequest request);
        /// <summary>Giờ chốt đơn của ngày ăn (20h ngày D-1, BusinessRules).</summary>
        public DateTime CutoffOf(DateTime mealDate);
        /// <summary>Nội bộ (duyệt ORDER_DEVIATION): ghi số suất mới, chạy trong transaction của người gọi; trả lỗi hoặc null.</summary>
        public Task<string?> ApplyLinesAsync(Guid orderId, IReadOnlyCollection<AppDelicare.Model.Request.Custom.OrderLineQuantityRequest> changes);
        /// <summary>Nội bộ: nội dung được duyệt của ORDER_DEVIATION (trạng thái + số suất từng dòng).</summary>
        public Task<string?> BuildDeviationStateAsync(Guid orderId);
        /// <summary>BE2.2 (BUSINESS_RULES §2): xin sửa đơn sau giờ chốt – tạo yêu cầu duyệt ORDER_CHANGE_AFTER_CUTOFF kèm đề xuất.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> RequestChange(AppDelicare.Model.Request.Custom.OrderChangeAfterCutoffRequest request);
        /// <summary>Nội bộ (handler ORDER_CHANGE_AFTER_CUTOFF) – chạy trong transaction của engine.</summary>
        public Task<string?> BuildChangeAfterCutoffStateAsync(Guid orderId);
        public Task<string?> ApplyChangeAfterCutoffAsync(Guid orderId, string? proposalJson, string? note);
        public Task<string?> RejectChangeAfterCutoffAsync(Guid orderId, string note);
        /// <summary>ORD-OPT-1.4 / 1.5 (D-74 g, h): đăng ký suất tuần theo lựa chọn + phiếu đăng ký Excel.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderWeekModel>> GetWeek(AppDelicare.Model.Request.Custom.OrderWeekRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderWeekSaveModel>> SaveWeek(AppDelicare.Model.Request.Custom.OrderWeekSaveRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderWeekModel>> CopyPreviousWeek(AppDelicare.Model.Request.Custom.OrderWeekRequest request);
        public Task<IResult<byte[]>> ExportWeekSlip(AppDelicare.Model.Request.Custom.OrderWeekRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderWeekSlipModel>> ImportWeekSlip(Guid customerId, Guid locationId, DateTime weekStart, System.IO.Stream file);
        /// <summary>UI-6.6 (D-85): tổng quan tuần mọi khách / điểm giao, xác nhận nhiều điểm, lấy số dự báo, tải nhiều phiếu.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderWeekOverviewModel>> GetWeekOverview(AppDelicare.Model.Model.OrderWeekOverviewRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderWeekConfirmResultModel>> ConfirmWeekPoints(AppDelicare.Model.Model.OrderWeekConfirmRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderWeekModel>> FillWeekFromForecast(AppDelicare.Model.Request.Custom.OrderWeekRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderWeekSlipBatchResultModel>> ImportWeekSlips(DateTime? weekStart, IReadOnlyList<(string Name, System.IO.Stream File)> files);
        /// <summary>UI-6.15 (D-96): đơn hàng theo ngày + thêm đơn phát sinh.</summary>
        public Task<IResult<AppDelicare.Model.Model.OrderDayModel>> GetDay(AppDelicare.Model.Model.OrderDayRequest request);
        public Task<IResult<AppDelicare.Model.Model.OrderDetailModel>> CreateExtra(AppDelicare.Model.Model.OrderExtraRequest request);
    }
}
