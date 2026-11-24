using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.4 (D-83): phụ lục điều chỉnh giá trong hợp đồng (dòng PricePolicy có ContractId) + duyệt qua engine.</summary>
    public interface IPriceAdjustmentService
    {
        Task<IResult<ContractPricesModel>> GetContractPrices(Guid contractId);
        Task<IResult<List<Guid>>> Create(PriceAdjustmentCreateRequest request);
        Task<IResult<string>> Submit(Guid id);
        Task<IResult<bool>> DeleteDraft(Guid id);

        // Engine duyệt (PriceAdjustmentApprovalHandler)
        Task<string?> BuildStateAsync(Guid id);
        Task<string?> DescribeAsync(Guid id);
        Task<ApprovalSummaryModel?> SummaryAsync(Guid id, AppDelicare.Services.Interface.ApprovalSummaryContext ctx);
        Task<string?> ApproveFromEngineAsync(Guid id, string? note);
        Task<string?> RejectFromEngineAsync(Guid id, string note);
    }
}
