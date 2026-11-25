using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IUserService
    {
        public Task<IResults<UserViewModel>> GetAll(UserSearchRequest request);
        public Task<IResult<bool>> Save(UserRequest request, string typeSave);
        /// <summary>T45R.8 (D-38, D-39): User/Insert – tạo tài khoản (chặn Person đã có User, UserName trùng), trả mật khẩu tạm một lần.</summary>
        public Task<IResult<PersonUserCreatedViewModel>> InsertAccount(UserRequest request);
        public IResults<User> GetAllData();
        public Task<IResult<UserModel>> GetById(Guid id);
        /// <summary>T45R.8 (D-39): Data = mật khẩu tạm 12 ký tự (hạn 24 giờ), trả một lần.</summary>
        public Task<IResult<string>> ResetPassword(Guid id);
        public Task<IResult<bool>> SaveIsActive(Guid id);
        public Task<IResult<bool>> SaveIsAdmin(Guid id);
        public Task<IResult<bool>> ChangePassword(ChangePasswordRequest request);
        /// <summary>T45R.8 (D-38): tạo đúng 1 tài khoản cho Person vừa tạo, trong transaction của nơi gọi.</summary>
        public Task<IResult<PersonUserCreatedViewModel>> InsertUserAfter(Guid personId, string? fullName, string typeSave
            , string? userName = null, string? listUserGroupId = null, bool isActive = false);
        /// <summary>T45R.8 (D-38): kiểm / tự sinh tên đăng nhập + nhóm quyền trước khi ghi. Trả thông báo lỗi (rỗng = hợp lệ).</summary>
        public string ValidateNewAccount(string? userName, string? fullName, string? listUserGroupId, out string resolvedUserName);
        /// <summary>T45R.8 (D-39): tài khoản đang dùng mật khẩu tạm.</summary>
        public Task<bool> IsTemporaryPassword(Guid userId);
        /// <summary>T45R: tài khoản đang đăng nhập + menu theo quyền.</summary>
        public Task<IResult<CurrentUserModel>> GetCurrent();
        // UI-6.42 (D-123)
        public Task<IResult<AppDelicare.Model.Model.AccountListModel>> GetList(AppDelicare.Model.Model.AccountListRequest request);
        public Task<IResult<AppDelicare.Model.Model.AccountBulkResultModel>> SetGroupsMany(AppDelicare.Model.Model.AccountIdsRequest request);
        public Task<IResult<AppDelicare.Model.Model.AccountBulkResultModel>> LockMany(AppDelicare.Model.Model.AccountIdsRequest request);
        public Task<IResults<AppDelicare.Model.Model.AccountPendingModel>> GetPendingRequests();
        public Task<IResult<AppDelicare.Model.Model.AccountOverviewModel>> GetOverview(Guid id);
    }
}
