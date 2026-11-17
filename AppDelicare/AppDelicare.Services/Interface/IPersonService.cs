using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IPersonService
    {
        public Task<IResults<PersonViewModel>> GetAll(PersonSearchRequest request);
        public Task<IResult<bool>> Save(PersonCustomRequest request, string typeSave);
        /// <summary>T45R.8 (D-38): Person/Insert – tạo hồ sơ + đúng 1 tài khoản (+ nhóm) trong 1 transaction, trả mật khẩu tạm một lần.</summary>
        public Task<IResult<PersonUserCreatedViewModel>> InsertPersonUser(PersonCustomRequest request);
        public IResults<Person> GetAllData();
        public Task<IResult<Person>> GetById(Guid id);
        public Task<IResults<PersonViewModel>> GetAllDataByKey(string key, bool isEdit);
        public IResult<PersonCustomViewModel> GetProfile();
        public Task<IResult<bool>> UpdateProfile(PersonCustomRequest request);
    }
}
