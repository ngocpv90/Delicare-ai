using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IUserClaimService
    {
        public Task<IResults<UserClaimViewModel>> GetAll(UserClaimSearchRequest request);
        public Task<IResult<bool>> Save(UserClaimRequest request, string typeSave);
        public IResults<UserClaim> GetAllData();
        public Task<IResult<UserClaim>> GetById(Guid id);
        public Task<IResult<bool>> SaveAfterUser(Guid userId, string? listUserGroupId);
    }
}
