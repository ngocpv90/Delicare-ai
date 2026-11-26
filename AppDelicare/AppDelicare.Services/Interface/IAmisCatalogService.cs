using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>ACC-1 (D-131 c): danh mục kế toán – khớp mã với MISA AMIS (AMIS là nguồn mã chuẩn).</summary>
    public interface IAmisCatalogService
    {
        /// <summary>Nhận danh mục từ tệp Excel xuất từ AMIS (kind trống → tự nhận theo tên sheet / tiêu đề cột); lưu bản chụp mới (JSON, tài liệu đính kèm).</summary>
        Task<IResult<AmisImportResultModel>> ImportExcel(Stream file, string? fileName, string? kind);

        /// <summary>So danh mục Delicare đang dùng với bản chụp AMIS mới nhất theo mã.</summary>
        Task<IResult<AmisMatchModel>> GetMatch(string? kind);

        /// <summary>Ghép mã: đổi mã Delicare (nguyên liệu / khách / NCC) thành mã AMIS (ghi AuditChange).</summary>
        Task<IResult<AmisMatchRowModel>> Remap(AmisRemapRequest request);

        /// <summary>Excel danh sách mã chưa có trên AMIS (mỗi loại 1 sheet) để kế toán tạo trên AMIS.</summary>
        Task<IResult<FileContentModel>> ExportMissing();

        /// <summary>Bản chụp danh mục AMIS mới nhất (null = chưa nhận lần nào) – dùng cho chứng từ (ĐVT theo AMIS – ACC-2).</summary>
        Task<AmisCatalogSnapshot?> GetSnapshotAsync();
    }
}
