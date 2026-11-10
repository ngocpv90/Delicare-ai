using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ICustomerContactService
    {
        public Task<IResults<CustomerContactViewModel>> GetAll(CustomerContactSearchRequest request);
        public Task<IResult<bool>> Save(CustomerContactRequest request, string typeSave);
        public IResults<CustomerContact> GetAllData();
        public Task<IResult<CustomerContact>> GetById(Guid id);
    }
}
