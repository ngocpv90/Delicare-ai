using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
	public interface IAdministrativeUnitService
    {
		public Task<IResults<AdministrativeUnitViewModel>> GetAll(AdministrativeUnitSearchRequest request);
        public Task<IResult<bool>> Save(AdministrativeUnitRequest request, string typeSave);
        public IResults<AdministrativeUnit> GetAllData();
        public Task<IResult<AdministrativeUnit>> GetById(Guid id);
	}
}
