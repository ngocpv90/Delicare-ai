using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>ACC-8 (D-131 l): Kế toán › Đối chiếu với AMIS – 8 chỉ tiêu Delicare ↔ AMIS theo kỳ.</summary>
    public interface IAmisReconService
    {
        Task<IResult<AmisReconModel>> GetMonth(string? month);
        Task<IResult<bool>> SaveFigures(AmisReconSaveRequest request);
        Task<IResult<FileContentModel>> Template(string? month);
        Task<IResult<bool>> ImportExcel(string? month, Stream stream, string fileName);
    }
}
