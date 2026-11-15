using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IBranchService
    {
        public Task<IResults<BranchViewModel>> GetAll(BranchSearchRequest request);
        public Task<IResult<bool>> Save(BranchRequest request, string typeSave);
        public IResults<Branch> GetAllData();
        public Task<IResult<Branch>> GetById(Guid id);
        public IResults<Branch> GetAllDataByRegionId(Guid? regionId);
        public IResults<Branch> GetAllDataByAreaId(Guid? areaId);
    }
}
