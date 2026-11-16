using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IAdministrativeWardService
    {
        public Task<IResults<AdministrativeWardViewModel>> GetAll(AdministrativeWardSearchRequest request);
        public Task<IResult<bool>> Save(AdministrativeWardRequest request, string typeSave);
        public IResults<AdministrativeWard> GetAllData();
        public Task<IResult<AdministrativeWard>> GetById(Guid id);
        public IResults<AdministrativeWard> GetAllDataByAdministrativeDistrictId(Guid? administrativeDistrictId);
    }
}
