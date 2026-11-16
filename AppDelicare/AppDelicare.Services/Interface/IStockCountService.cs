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
    public interface IStockCountService
    {
        public Task<IResults<StockCountViewModel>> GetAll(StockCountSearchRequest request);
        public Task<IResult<bool>> Save(StockCountRequest request, string typeSave);
        public IResults<StockCount> GetAllData();
        public Task<IResult<StockCount>> GetById(Guid id);
        /// <summary>T16: tạo phiếu kiểm kê (số PH-, dòng từ tồn hệ thống).</summary>
        public Task<IResult<StockCountDetailModel>> Create(StockCountCreateRequest request);
        /// <summary>T16: ghi số đếm thực tế, tính chênh lệch.</summary>
        public Task<IResult<StockCountDetailModel>> RecordCounts(StockCountRecordRequest request);
        /// <summary>T16: ghi sổ điều chỉnh (ADJUSTMENT); lệch vượt ngưỡng chỉ admin (Pilot).</summary>
        public Task<IResult<StockCountDetailModel>> Post(InventoryDocumentActionRequest request);
        /// <summary>T16: chi tiết phiếu kiểm kê.</summary>
        public Task<IResult<StockCountDetailModel>> GetDetail(Guid id);
    }
}
