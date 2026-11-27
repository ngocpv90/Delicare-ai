using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>RETAIL-2 (D-136): tài khoản khách lẻ trên trang web + Đơn của tôi (tiến độ, chuyến giao, nguồn gốc).</summary>
    public interface IRetailAccountService
    {
        Task<IResult<RetailSessionModel>> Register(RetailRegisterRequest request, string? clientIp);
        Task<IResult<RetailSessionModel>> Login(RetailLoginRequest request, string? clientIp);
        /// <summary>Phiên hợp lệ → khách (đang hoạt động); không thì null.</summary>
        Task<RetailCustomer?> Authenticate(string? token);
        Task<IResult<RetailCustomerProfileModel>> GetProfile(string? token);
        Task<IResult<RetailCustomerProfileModel>> UpdateProfile(string? token, RetailProfileRequest request);
        Task<IResult<RetailSessionModel>> ChangePassword(string? token, RetailChangePasswordRequest request);
        /// <summary>group: active (chưa giao xong) / done (đã giao) / cancelled / all.</summary>
        Task<IResult<RetailMyOrderListModel>> GetMyOrders(string? token, string? group);
        Task<IResult<RetailMyOrderDetailModel>> GetMyOrder(string? token, string? orderNumber);
        Task<IResult<RetailTraceModel>> GetOrderTrace(string? token, string? orderNumber);
        Task<IResult<RetailMyOrderDetailModel>> ClaimOrder(string? token, RetailClaimOrderRequest request);
    }
}
