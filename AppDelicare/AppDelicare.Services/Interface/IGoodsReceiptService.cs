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
    public interface IGoodsReceiptService
    {
        public Task<IResults<GoodsReceiptViewModel>> GetAll(GoodsReceiptSearchRequest request);
        public Task<IResult<bool>> Save(GoodsReceiptRequest request, string typeSave);
        public IResults<GoodsReceipt> GetAllData();
        public Task<IResult<GoodsReceipt>> GetById(Guid id);
        /// <summary>T15 (spec 7.3 receive): lập phiếu nhập Chờ QC theo PO đã duyệt (+ phiếu QC).</summary>
        public Task<IResult<GoodsReceiptDetailModel>> Create(GoodsReceiptCreateRequest request);
        /// <summary>T15 (spec 7.4 post): ghi sổ phiếu đã kết luận QC – lô NL-, nhập kho phần đạt, nhiệt độ nhận, sự cố NCC, cập nhật PO.</summary>
        public Task<IResult<GoodsReceiptDetailModel>> Post(GoodsReceiptPostRequest request);
        /// <summary>T15: hủy phiếu chưa ghi sổ (lý do bắt buộc).</summary>
        public Task<IResult<GoodsReceiptDetailModel>> Cancel(PurchaseCancelRequest request);
        public Task<IResult<GoodsReceiptDetailModel>> GetDetail(Guid id);
        /// <summary>T15: chi tiết dùng chung cho QcInspection/GetDetail.</summary>
        public Task<GoodsReceiptDetailModel?> DetailAsync(Guid id);
    }
}
