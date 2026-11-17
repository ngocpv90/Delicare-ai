using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProductionBatchService
    {
        public Task<IResults<ProductionBatchViewModel>> GetAll(ProductionBatchSearchRequest request);
        public Task<IResult<bool>> Save(ProductionBatchRequest request, string typeSave);
        public IResults<ProductionBatch> GetAllData();
        public Task<IResult<ProductionBatch>> GetById(Guid id);
        /// <summary>BE2.1 (spec 7.5 trace – chiều ngược từ mẻ).</summary>
        public Task<IResult<AppDelicare.Model.Model.TraceModel>> Trace(Guid id);
        // T17 (Pilot P7): bảng mẻ, chi tiết, xuất NL cho mẻ (FEFO), bắt đầu / xong, nhiệt độ lõi, lưu mẫu, sản lượng, hao hụt, QC đạt / giữ / loại, đóng gói, in Phiếu sản xuất.
        public Task<IResult<ProductionBoardModel>> GetBoard(ProductionBoardRequest request);
        public Task<IResult<ProductionBatchDetailModel>> GetDetail(Guid id);
        // BE2.4 (T17 công đoạn theo tổ): bắt đầu / xong công đoạn, tiến độ bếp theo tổ.
        public Task<IResult<ProductionBatchDetailModel>> StartStage(AppDelicare.Model.Request.Custom.ProductionStageRequest request);
        public Task<IResult<ProductionBatchDetailModel>> FinishStage(AppDelicare.Model.Request.Custom.ProductionStageRequest request);
        public Task<IResult<ProductionStageBoardModel>> GetStageBoard(AppDelicare.Model.Request.Custom.ProductionStageBoardRequest request);
        public Task<IResult<MaterialIssueDetailModel>> CreateMaterialIssue(ProductionBatchIssueRequest request);
        public Task<IResult<ProductionBatchDetailModel>> Start(ProductionBatchStartRequest request);
        public Task<IResult<ProductionBatchDetailModel>> Finish(ProductionBatchFinishRequest request);
        public Task<IResult<ProductionBatchDetailModel>> RecordCoreTemperature(ProductionBatchTemperatureRequest request);
        public Task<IResult<ProductionBatchDetailModel>> RecordSampleRetained(ProductionBatchSampleRequest request);
        public Task<IResult<ProductionBatchDetailModel>> RecordOutput(ProductionBatchOutputRequest request);
        public Task<IResult<ProductionBatchDetailModel>> RecordWaste(ProductionBatchWasteRequest request);
        public Task<IResult<ProductionBatchDetailModel>> Release(ProductionActionRequest request);
        public Task<IResult<ProductionBatchDetailModel>> Hold(ProductionActionRequest request);
        public Task<IResult<ProductionBatchDetailModel>> Reject(ProductionActionRequest request);
        public Task<IResult<ProductionBatchDetailModel>> Pack(ProductionActionRequest request);
        public Task<IResult<byte[]>> ExportProductionSheet(Guid id);
    }
}
