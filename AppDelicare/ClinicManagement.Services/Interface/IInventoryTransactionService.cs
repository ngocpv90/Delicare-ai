using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IInventoryTransactionService
    {
        public Task<IResults<InventoryTransactionViewModel>> GetAll(InventoryTransactionSearchRequest request);
        public Task<IResult<bool>> Save(InventoryTransactionRequest request, string typeSave);
        public IResults<InventoryTransaction> GetAllData();
        public Task<IResult<InventoryTransaction>> GetById(Guid id);
    }
}
