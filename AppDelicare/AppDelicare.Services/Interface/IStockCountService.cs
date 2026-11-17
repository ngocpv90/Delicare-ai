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
        // BE2.3 (spec 7.8 submit / post, 2.3) – duyệt kiểm kê lệch > ngưỡng qua engine (STOCK_COUNT, 2 cấp).
        public Task<IResult<StockCountDetailModel>> Submit(InventoryDocumentActionRequest request);
        public Task<IResult<bool>> Decide(InventoryDocumentActionRequest request, bool approve);
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        public Task<string?> ApplyDecisionAsync(Guid id, bool approved, Guid actorUserId, string? note);
        /// <summary>T16: chi tiết phiếu kiểm kê.</summary>
        public Task<IResult<StockCountDetailModel>> GetDetail(Guid id);
    }
}
