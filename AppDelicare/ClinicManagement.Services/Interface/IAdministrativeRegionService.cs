using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
	public interface IAdministrativeRegionService
    {
		public Task<IResults<AdministrativeRegionViewModel>> GetAll(AdministrativeRegionSearchRequest request);
        public Task<IResult<bool>> Save(AdministrativeRegionRequest request, string typeSave);
        public IResults<AdministrativeRegion> GetAllData();
        public Task<IResult<AdministrativeRegion>> GetById(Guid id);
	}
}
