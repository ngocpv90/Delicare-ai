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
    public interface ICleaningBatchService
    {
        public Task<IResults<CleaningBatchViewModel>> GetAll(CleaningBatchSearchRequest request);
        public Task<IResult<bool>> Save(CleaningBatchRequest request, string typeSave);
        public IResults<CleaningBatch> GetAllData();
        public Task<IResult<CleaningBatch>> GetById(Guid id);

        /// <summary>BE2.5 (spec 9.4): chi tiết mẻ vệ sinh + 6 bước.</summary>
        public Task<IResult<CleaningBatchDetailModel>> GetDetail(Guid id);
        /// <summary>BE2.5 (Catalog 167): lập mẻ vệ sinh từ phiếu kiểm tra thu hồi.</summary>
        public Task<IResult<CleaningBatchDetailModel>> Create(CleaningBatchCreateRequest request);
        /// <summary>BE2.5 (Catalog 168): ghi kết quả các bước vệ sinh.</summary>
        public Task<IResult<CleaningBatchDetailModel>> RecordResults(CleaningRecordRequest request);
        /// <summary>BE2.5 (Catalog 169): kết luận mẻ vệ sinh PASS / REJECT.</summary>
        public Task<IResult<CleaningBatchDetailModel>> Complete(CleaningCompleteRequest request);
    }
}
