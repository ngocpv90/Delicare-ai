using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrderLineService
    {
        public Task<IResults<OrderLineViewModel>> GetAll(OrderLineSearchRequest request);
        public Task<IResult<bool>> Save(OrderLineRequest request, string typeSave);
        public IResults<OrderLine> GetAllData();
        public Task<IResult<OrderLine>> GetById(Guid id);
    }
}
