using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IDepartmentService
    {
        public Task<IResults<DepartmentViewModel>> GetAll(DepartmentSearchRequest request);
        public Task<IResult<bool>> Save(DepartmentRequest request, string typeSave);
        public IResults<Department> GetAllData();
        public Task<IResult<Department>> GetById(Guid id);
        public IResults<Department> GetAllDataByBranchId(Guid? branchId);
    }
}
