using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// T49: nạp dữ liệu danh mục ban đầu từ Excel – file mẫu từng danh mục và file vận hành thật (Rule Menu, Tổng hợp báo giá).
    /// Idempotent theo mã, kiểm tra từng dòng (dòng lỗi bỏ qua, báo rõ; dòng hợp lệ vẫn nạp).
    /// </summary>
    public interface IMasterDataImportService
    {
        public IResults<ImportTemplateModel> GetTemplateList();
        /// <summary>Màn hình cần quyền Import của loại file; null nếu không có loại này.</summary>
        public string? ScreenOf(string? tableName);
        /// <summary>File mẫu .xlsx (dòng tiêu đề + 1 dòng ví dụ [DEMO]); null nếu loại này không có mẫu.</summary>
        public byte[]? BuildTemplate(string? tableName);
        public Task<IResult<ImportResultModel>> Import(string? tableName, Stream file);
    }
}
