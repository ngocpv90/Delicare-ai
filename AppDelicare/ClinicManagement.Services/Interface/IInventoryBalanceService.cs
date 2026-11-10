using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IInventoryBalanceService
    {
        public Task<IResults<InventoryBalanceViewModel>> GetAll(InventoryBalanceSearchRequest request);
        public Task<IResult<bool>> Save(InventoryBalanceRequest request, string typeSave);
        public IResults<InventoryBalance> GetAllData();
        public Task<IResult<InventoryBalance>> GetById(Guid id);
    }
}
