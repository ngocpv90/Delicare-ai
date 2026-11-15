using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IDeliveryRouteService
    {
        public Task<IResults<DeliveryRouteViewModel>> GetAll(DeliveryRouteSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryRouteRequest request, string typeSave);
        public IResults<DeliveryRoute> GetAllData();
        public Task<IResult<DeliveryRoute>> GetById(Guid id);
    }
}
