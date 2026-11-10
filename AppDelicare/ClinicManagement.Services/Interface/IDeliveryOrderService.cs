using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IDeliveryOrderService
    {
        public Task<IResults<DeliveryOrderViewModel>> GetAll(DeliveryOrderSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryOrderRequest request, string typeSave);
        public IResults<DeliveryOrder> GetAllData();
        public Task<IResult<DeliveryOrder>> GetById(Guid id);
    }
}
