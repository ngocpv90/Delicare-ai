using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
	public interface IAdministrativeProvinceService
    {
		public Task<IResults<AdministrativeProvinceViewModel>> GetAll(AdministrativeProvinceSearchRequest request);
        public Task<IResult<bool>> Save(AdministrativeProvinceRequest request, string typeSave);
        public IResults<AdministrativeProvince> GetAllData();
        public Task<IResult<AdministrativeProvince>> GetById(Guid id);
	}
}
