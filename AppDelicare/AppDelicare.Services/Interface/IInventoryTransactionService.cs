using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IInventoryTransactionService
    {
        public Task<IResults<InventoryTransactionViewModel>> GetAll(InventoryTransactionSearchRequest request);
        public Task<IResult<bool>> Save(InventoryTransactionRequest request, string typeSave);
        public IResults<InventoryTransaction> GetAllData();
        public Task<IResult<InventoryTransaction>> GetById(Guid id);
        // <summary>T45R: đảo bút toán kho.</summary>
        public Task<IResult<InventoryTransaction>> Reverse(InventoryReverseRequest request);
        /// <summary>T16: sổ cái có lọc khoảng ngày (SP uspInventoryTransaction_GetLedger).</summary>
        public Task<IResults<InventoryTransactionViewModel>> GetLedger(InventoryLedgerSearchRequest request);
        /// <summary>T16: bút toán điều chỉnh tay (ADJUSTMENT, số PH-, lý do bắt buộc).</summary>
        public Task<IResult<InventoryTransaction>> Adjust(InventoryAdjustRequest request);
        /// <summary>T16: chuyển kho / vị trí (cặp TRANSFER_OUT / TRANSFER_IN).</summary>
        public Task<IResults<InventoryTransaction>> Transfer(InventoryTransferRequest request);
    }
}
