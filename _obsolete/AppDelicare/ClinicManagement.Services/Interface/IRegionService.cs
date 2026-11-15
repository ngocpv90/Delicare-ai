using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IRegionService
    {
        public Task<IResults<RegionViewModel>> GetAll(RegionSearchRequest request);
        public Task<IResult<bool>> Save(RegionRequest request, string typeSave);
        public IResults<Region> GetAllData();
        public Task<IResult<Region>> GetById(Guid id);
    }
}
