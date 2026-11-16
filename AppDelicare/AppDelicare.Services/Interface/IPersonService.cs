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
        public IResults<Person> GetAllData();
        public Task<IResult<Person>> GetById(Guid id);
        public Task<IResults<PersonViewModel>> GetAllDataByKey(string key, bool isEdit);
        public IResult<PersonCustomViewModel> GetProfile();
        public Task<IResult<bool>> UpdateProfile(PersonCustomRequest request);
    }
}
