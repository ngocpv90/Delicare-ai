using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ICustomerDeliveryLocationService
    {
        public Task<IResults<CustomerDeliveryLocationViewModel>> GetAll(CustomerDeliveryLocationSearchRequest request);
        public Task<IResult<bool>> Save(CustomerDeliveryLocationRequest request, string typeSave);
        public IResults<CustomerDeliveryLocation> GetAllData();
        public Task<IResult<CustomerDeliveryLocation>> GetById(Guid id);
    }
}
