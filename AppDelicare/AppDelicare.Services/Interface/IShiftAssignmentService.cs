// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6) – CodeGen không ghi đè.
#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IShiftAssignmentService
    {
        public Task<IResults<ShiftAssignmentViewModel>> GetAll(ShiftAssignmentSearchRequest request);
        public Task<IResult<bool>> Save(ShiftAssignmentRequest request, string typeSave);
        public IResults<ShiftAssignment> GetAllData();
        public Task<IResult<ShiftAssignment>> GetById(Guid id);

        // BE2.6 (spec 11.1, Catalog 201–204) – phân ca.
        /// <summary>Lưới ca nhân viên × ngày + số liệu hôm nay theo ca.</summary>
        public Task<IResult<ShiftGridModel>> GetGrid(ShiftGridRequest request);
        /// <summary>Phân / đổi / bỏ ca 1 nhân viên 1 ngày (upsert).</summary>
        public Task<IResult<ShiftAssignResultModel>> Assign(ShiftAssignRequest request);
        /// <summary>Phân ca hàng loạt (nhân viên × khoảng ngày).</summary>
        public Task<IResult<ShiftBulkAssignResultModel>> BulkAssign(ShiftBulkAssignRequest request);
        /// <summary>Kế hoạch → Xác nhận (không gửi Telegram).</summary>
        public Task<IResult<ShiftConfirmResultModel>> Confirm(ShiftConfirmRequest request);
        /// <summary>Đánh Vắng ca đã hết giờ mà không vào ca (thao tác tay – D-45).</summary>
        public Task<IResult<ShiftMarkAbsentResultModel>> MarkAbsent(ShiftMarkAbsentRequest request);
    }
}
