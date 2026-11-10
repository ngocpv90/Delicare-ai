using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPackingOrderService
    {
        public Task<IResults<PackingOrderViewModel>> GetAll(PackingOrderSearchRequest request);
        public Task<IResult<bool>> Save(PackingOrderRequest request, string typeSave);
        public IResults<PackingOrder> GetAllData();
        public Task<IResult<PackingOrder>> GetById(Guid id);
    }
}
