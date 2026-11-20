using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IEmployeeService
    {
        /// <summary>UI-4 (D-68 g1): hồ sơ nhân viên cho màn chi tiết – thông tin cá nhân chỉ khi người xem có quyền Sửa adm-employee.</summary>
        public Task<IResult<AppDelicare.Model.Model.EmployeeOverviewModel>> GetOverview(Guid id);
        public Task<IResults<EmployeeViewModel>> GetAll(EmployeeSearchRequest request);
        public Task<IResult<bool>> Save(EmployeeRequest request, string typeSave);
        public IResults<Employee> GetAllData();
        public Task<IResult<Employee>> GetById(Guid id);
    }
}
