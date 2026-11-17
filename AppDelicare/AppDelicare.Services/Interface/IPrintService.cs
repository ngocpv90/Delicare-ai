using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// BE2.7 (spec 2.5, Catalog 24–30): mẫu in phiếu – pdf (mặc định, PDFsharp-MigraDoc – BE2.7.1, D-56 k), xlsx (ClosedXML) hoặc docx (GemBox.Document). Action <c>Print</c> nằm ở controller của chứng từ
    /// (quyền In theo PermissionConvention); in hàng loạt <c>Common/PrintBatch</c> kiểm quyền In từng loại phiếu ở đây.
    /// </summary>
    public interface IPrintService
    {
        /// <summary>Loại phiếu → màn hình chịu quyền In.</summary>
        public IReadOnlyDictionary<string, string> TypeScreens { get; }
        /// <summary>Dựng nội dung 1 phiếu (dùng cho test / xem trước); lỗi nghiệp vụ → Fail 400.</summary>
        public Task<IResult<PrintDocumentModel>> Build(string type, Guid id);
        /// <summary>In 1 phiếu (format: pdf – mặc định / xlsx / docx).</summary>
        public Task<IResult<FileContentModel>> Print(string type, Guid id, string? format);
        /// <summary>In hàng loạt (tối đa BusinessRules:Common:PrintBatchMaxItems) → 1 tệp gộp; quyền In theo từng loại phiếu.</summary>
        public Task<IResult<FileContentModel>> PrintBatch(PrintBatchRequest request);
    }
}
