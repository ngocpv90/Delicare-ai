using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>RETAIL-1: API công khai của trang khách lẻ (không đăng nhập) – docs/frontend-handoff/RETAIL-1.md mục 5.1.</summary>
    public interface IRetailPortalService
    {
        Task<IResult<RetailCatalogModel>> GetCatalog();
        Task<IResult<RetailWeeklyMenuModel>> GetWeeklyMenu();
        /// <summary>ResponseCode 429 khi vượt giới hạn gửi đơn.</summary>
        Task<IResult<RetailSubmitResultModel>> SubmitOrder(RetailSubmitOrderRequest request, string? clientIp, Guid? retailCustomerId = null);
        /// <summary>ResponseCode 429 khi tra cứu quá nhiều.</summary>
        Task<IResult<RetailMyOrderDetailModel>> TrackOrder(string? orderNumber, string? phoneNumber, string? clientIp);
    }
}
