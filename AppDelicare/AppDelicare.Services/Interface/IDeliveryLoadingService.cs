using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IDeliveryLoadingService
    {
        public Task<IResults<DeliveryLoadingViewModel>> GetAll(DeliveryLoadingSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryLoadingRequest request, string typeSave);
        public IResults<DeliveryLoading> GetAllData();
        public Task<IResult<DeliveryLoading>> GetById(Guid id);
    }
}
