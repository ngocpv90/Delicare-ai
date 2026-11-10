using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IUserService
    {
        public Task<IResults<UserViewModel>> GetAll(UserSearchRequest request);
        public Task<IResult<bool>> Save(UserRequest request, string typeSave);
        public IResults<User> GetAllData();
        public Task<IResult<UserModel>> GetById(Guid id);
        public Task<IResult<bool>> ResetPassword(Guid id);
        public Task<IResult<bool>> SaveIsActive(Guid id);
        public Task<IResult<bool>> SaveIsAdmin(Guid id);
        public Task<IResult<bool>> ChangePassword(ChangePasswordRequest request);
        public Task<bool> InsertUserAfter(Guid personId, string? fullName, string typeSave);
    }
}
