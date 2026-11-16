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
        // <summary>T13 + T46B.1 (spec 6.2): lịch tuần, đổi trạng thái, sao chép, in thực đơn gửi khách.</summary>
        public Task<IResult<AppDelicare.Model.Model.MenuCalendarModel>> GetCalendar(Guid id, string? mealType, decimal? sellingPrice, Guid? customerId, Guid? contractId, DateTime? on);
        public Task<IResult<bool>> Approve(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<bool>> Publish(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<bool>> Expire(AppDelicare.Model.Request.Custom.MenuActionRequest request);
        public Task<IResult<Menu>> Clone(AppDelicare.Model.Request.Custom.MenuCloneRequest request);
        public Task<IResult<byte[]>> ExportForCustomer(Guid id, string? project);
    }
}
