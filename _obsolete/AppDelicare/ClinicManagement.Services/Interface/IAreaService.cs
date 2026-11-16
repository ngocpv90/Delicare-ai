using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IAreaService
    {
        public Task<IResults<AreaViewModel>> GetAll(AreaSearchRequest request);
        public Task<IResult<bool>> Save(AreaRequest request, string typeSave);
        public IResults<Area> GetAllData();
        public Task<IResult<Area>> GetById(Guid id);
        public IResults<Area> GetAllDataByRegionId(Guid? regionId);
    }
}
