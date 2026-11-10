using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrderService
    {
        public Task<IResults<OrderViewModel>> GetAll(OrderSearchRequest request);
        public Task<IResult<bool>> Save(OrderRequest request, string typeSave);
        public IResults<Order> GetAllData();
        public Task<IResult<Order>> GetById(Guid id);
    }
}
