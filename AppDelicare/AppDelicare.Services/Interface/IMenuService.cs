using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMenuService
    {
        public Task<IResults<MenuViewModel>> GetAll(MenuSearchRequest request);
        public Task<IResult<bool>> Save(MenuRequest request, string typeSave);
        public IResults<Menu> GetAllData();
        public Task<IResult<Menu>> GetById(Guid id);
        /// <summary>BE2.1 (spec 6.2 GET menus): thực đơn + lineCount, assignedCustomerCount, orderedMeals.</summary>
        public Task<IResults<AppDelicare.Model.Model.MenuListItemModel>> GetList(AppDelicare.Model.Request.Custom.MenuListRequest request);
        // <summary>T13 + T46B.1 (spec 6.2): lịch tuần, đổi trạng thái, sao chép, in thực đơn gửi khách.</summary>
        public Task<IResult<AppDelicare.Model.Model.MenuCalendarModel>> GetCalendar(Guid id, string? mealType, decimal? sellingPrice, Guid? customerId, Guid? contractId, DateTime? on);
        public Task<IResult<bool>> Approve(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<bool>> Publish(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<bool>> Expire(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<Menu>> Clone(AppDelicare.Model.Request.Custom.MenuCloneRequest request);
        public Task<IResult<byte[]>> ExportForCustomer(Guid id, string? project);
        /// <summary>UI-6.11 (D-89): thực đơn tuần – lưới, sửa ô, gợi ý món, điền hàng, chép ngày, gửi duyệt, tự lập nháp.</summary>
        public Task<IResult<AppDelicare.Model.Model.MenuWeekModel>> GetWeek(AppDelicare.Model.Model.MenuWeekRequest request);
        public Task<IResult<AppDelicare.Model.Model.MenuWeekModel>> SaveCell(AppDelicare.Model.Model.MenuCellRequest request);
        public Task<IResult<AppDelicare.Model.Model.MenuWeekModel>> FillRow(AppDelicare.Model.Model.MenuFillRowRequest request);
        public Task<IResult<AppDelicare.Model.Model.MenuWeekModel>> CopyDay(AppDelicare.Model.Model.MenuCopyDayRequest request);
        public Task<IResults<AppDelicare.Model.Model.MenuDishSuggestionModel>> SuggestDish(AppDelicare.Model.Model.MenuSuggestRequest request);
        public Task<IResult<AppDelicare.Model.Model.MenuWeekModel>> SubmitWeek(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<AppDelicare.Model.Model.MenuAutoDraftResultModel>> AutoDraft(DateTime? weekStart, Guid? sourceId = null, bool blank = false);
    }
}
