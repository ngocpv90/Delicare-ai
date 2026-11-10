using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IAdministrativeDistrictService
    {
        public Task<IResults<AdministrativeDistrictViewModel>> GetAll(AdministrativeDistrictSearchRequest request);
        public Task<IResult<bool>> Save(AdministrativeDistrictRequest request, string typeSave);
        public IResults<AdministrativeDistrict> GetAllData();
        public Task<IResult<AdministrativeDistrict>> GetById(Guid id);
        public IResults<AdministrativeDistrict> GetAllDataByAdministrativeProvinceId(Guid? administrativeProvinceId);
    }
}
