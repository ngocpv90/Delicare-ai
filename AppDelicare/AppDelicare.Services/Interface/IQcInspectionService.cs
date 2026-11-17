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
    public interface IQcInspectionService
    {
        public Task<IResults<QcInspectionViewModel>> GetAll(QcInspectionSearchRequest request);
        public Task<IResult<bool>> Save(QcInspectionRequest request, string typeSave);
        public IResults<QcInspection> GetAllData();
        public Task<IResult<QcInspection>> GetById(Guid id);
        /// <summary>T15 (spec 9.1 GET): chi tiết phiếu QC (= chi tiết phiếu nhập, kèm ngưỡng nhiệt độ và kết quả gợi ý).</summary>
        public Task<IResult<GoodsReceiptDetailModel>> GetDetail(Guid id);
        /// <summary>T15 (spec 9.1 conclude): kết luận QC từng dòng DAT / DAT_MOT_PHAN / KHONG_DAT.</summary>
        public Task<IResult<GoodsReceiptDetailModel>> Conclude(QcInspectionConcludeRequest request);
    }
}
