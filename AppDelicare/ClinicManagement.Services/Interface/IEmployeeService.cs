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
        public Task<IResults<EmployeeViewModel>> GetAll(EmployeeSearchRequest request);
        public Task<IResult<bool>> Save(EmployeeRequest request, string typeSave);
        public IResults<Employee> GetAllData();
        public Task<IResult<Employee>> GetById(Guid id);
    }
}
