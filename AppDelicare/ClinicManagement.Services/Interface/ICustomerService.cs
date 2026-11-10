using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ICustomerService
    {
        public Task<IResults<CustomerViewModel>> GetAll(CustomerSearchRequest request);
        public Task<IResult<bool>> Save(CustomerRequest request, string typeSave);
        public IResults<Customer> GetAllData();
        public Task<IResult<Customer>> GetById(Guid id);
    }
}
