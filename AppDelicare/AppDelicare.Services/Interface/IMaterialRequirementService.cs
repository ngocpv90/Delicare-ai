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
    public interface IMaterialRequirementService
    {
        public Task<IResults<MaterialRequirementViewModel>> GetAll(MaterialRequirementSearchRequest request);
        public Task<IResult<bool>> Save(MaterialRequirementRequest request, string typeSave);
        public IResults<MaterialRequirement> GetAllData();
        public Task<IResult<MaterialRequirement>> GetById(Guid id);
        /// <summary>T14: tính nhu cầu NL của 1 ngày sản xuất + đề nghị mua nháp (1 transaction).</summary>
        public Task<IResult<MaterialRequirementResultModel>> Calculate(MaterialRequirementCalculateRequest request);
        /// <summary>T14.1: đề nghị mua đã gửi duyệt / duyệt / lập PO → tính lại và lập đề nghị mua bổ sung Nháp cho phần thiếu thêm (1 transaction).</summary>
        public Task<IResult<MaterialRequirementResultModel>> CalculateTopUp(MaterialRequirementCalculateRequest request);
        /// <summary>BE2.1 (spec 7.1 create-purchase-request): đề nghị mua Nháp từ dòng thiếu được chọn + % dự phòng.</summary>
        public Task<IResult<MaterialRequirementResultModel>> CreatePurchaseRequest(MaterialRequirementPurchaseRequest request);
        /// <summary>T14: kết quả đã lưu theo ngày sản xuất (+ chi nhánh).</summary>
        public Task<IResult<MaterialRequirementResultModel>> GetByDate(MaterialRequirementByDateRequest request);
        /// <summary>BIZ-1.3: nhu cầu nhiều ngày (đơn chốt → đăng ký → dự báo), trừ tồn lũy kế, ngày phải đặt theo lead time NCC – chỉ tính khi xem.</summary>
        public Task<IResult<AppDelicare.Model.Model.MaterialHorizonModel>> GetHorizon(MaterialHorizonRequest request);
        /// <summary>BIZ-1.3: lập đề nghị mua Nháp cho phần phải đặt (dòng theo ngày cần hàng).</summary>
        public Task<IResult<AppDelicare.Model.Model.MaterialHorizonModel>> CreateHorizonPurchaseRequest(MaterialHorizonPurchaseRequest request);

        /// <summary>UI-6.51 (D-137): kế hoạch mua 7 ngày cho job / màn Kế hoạch mua – <paramref name="excludePurchaseRequestIds"/> = đề nghị Nháp của job sẽ được ghi đè (không trừ);
        /// <paramref name="detailDate"/> = ngày cần lấy thêm lượng mua theo món (trang chi tiết ô).</summary>
        public Task<(AppDelicare.Model.Model.MaterialHorizonModel? Model, string? Error)> PlanHorizonAsync(MaterialHorizonRequest request, IReadOnlyCollection<Guid>? excludePurchaseRequestIds = null, DateTime? detailDate = null);
        /// <summary>T14: kết quả đã lưu theo kế hoạch sản xuất.</summary>
        public Task<IResult<MaterialRequirementResultModel>> GetDetail(Guid productionPlanId);
        /// <summary>T15: in Phiếu trước sản xuất (.xlsx) từ kết quả tính nhu cầu của kế hoạch.</summary>
        public Task<IResult<byte[]>> ExportPreProduction(Guid productionPlanId);
    }
}
