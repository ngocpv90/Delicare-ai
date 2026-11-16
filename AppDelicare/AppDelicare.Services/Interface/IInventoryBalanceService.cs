using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
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
        /// <summary>T16: tồn theo kho × nguyên liệu (khả dụng, xuất được, sắp hết hạn, mức tồn).</summary>
        public Task<IResults<InventorySummaryModel>> GetSummary(InventorySummaryRequest request);
        /// <summary>T16: tồn theo lô, sắp FEFO / FIFO, lý do chặn xuất.</summary>
        public Task<IResults<InventoryLotBalanceModel>> GetByLot(InventoryByLotRequest request);
        /// <summary>T16: tính lại tồn từ sổ cái (chỉ admin).</summary>
        public Task<IResult<InventoryRebuildModel>> RebuildBalance(InventoryRebuildRequest request);
    }
}
