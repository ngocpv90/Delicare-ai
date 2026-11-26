using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>ACC-6 (D-131 j): định mức bao bì theo loại suất (công thức món bao bì) + tự xuất bao bì khi đóng gói.</summary>
    public interface IPackagingNormService
    {
        Task<IResult<PackagingNormMatrixModel>> GetMatrix();
        Task<IResult<List<PackagingSupplyModel>>> GetSupplies(string? search);
        Task<IResult<bool>> SaveMeal(PackagingNormSaveRequest request);
        Task<IResult<bool>> Submit(PackagingNormSubmitRequest request);
        Task<List<string>> AfterPackingAsync(DateTime day);
        Task<(int Posted, List<string> Failed)> AutoPostAsync(DateTime now);
        Task<IResult<string>> CreateShortagePurchaseRequest(DateTime day, string? shiftCode);
    }
}
