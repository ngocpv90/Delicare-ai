using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>ACC-4 (D-131 h): Kế toán › Khóa kỳ – điều kiện, khóa, xin mở khóa (COO duyệt), chứng từ điều chỉnh.</summary>
    public interface IAccountingPeriodService
    {
        Task<IResult<PeriodOverviewModel>> GetOverview(string? month);
        Task<IResult<bool>> Lock(PeriodActionRequest request);
        Task<IResult<string>> RequestUnlock(PeriodActionRequest request);
        Task<string?> ApplyUnlockAsync(Guid periodId, Guid actorUserId, string? proposalJson, string? note);
    }
}
