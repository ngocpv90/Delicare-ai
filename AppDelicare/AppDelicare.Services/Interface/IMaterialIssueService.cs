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
    public interface IMaterialIssueService
    {
        /// <summary>BIZ-2.5: nhóm phiếu xuất theo ca (thẻ [XUAT_CA:…]) từ ngày cho trước.</summary>
        public Task<IResults<AppDelicare.Model.Model.MaterialIssueShiftGroupModel>> GetShiftGroups(DateTime? fromDate);
        /// <summary>BIZ-2.5: ghi sổ mọi phiếu Nháp của ca.</summary>
        public Task<IResult<int>> PostShift(AppDelicare.Model.Request.Custom.MaterialIssueShiftRequest request);
        public Task<IResults<MaterialIssueViewModel>> GetAll(MaterialIssueSearchRequest request);
        public Task<IResult<bool>> Save(MaterialIssueRequest request, string typeSave);
        public IResults<MaterialIssue> GetAllData();
        public Task<IResult<MaterialIssue>> GetById(Guid id);
        /// <summary>T16: gợi ý lô FEFO / FIFO cho 1 nguyên liệu.</summary>
        public Task<IResult<LotSuggestionModel>> SuggestLots(MaterialIssueSuggestRequest request);
        /// <summary>T16: tạo phiếu xuất Nháp (số PH-, kiểm lô, cảnh báo FEFO).</summary>
        public Task<IResult<MaterialIssueDetailModel>> Create(MaterialIssueCreateRequest request);
        /// <summary>T16: ghi sổ phiếu xuất (ISSUE), Draft → Issued.</summary>
        public Task<IResult<MaterialIssueDetailModel>> Post(InventoryDocumentActionRequest request);
        /// <summary>T16: hủy phiếu xuất (đã xuất → bút toán đảo).</summary>
        public Task<IResult<MaterialIssueDetailModel>> Cancel(MaterialIssueCancelRequest request);
        /// <summary>T16: chi tiết phiếu xuất.</summary>
        public Task<IResult<MaterialIssueDetailModel>> GetDetail(Guid id);
    }
}
