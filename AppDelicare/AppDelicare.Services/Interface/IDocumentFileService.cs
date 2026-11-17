using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    public interface IDocumentFileService
    {
        public Task<IResult<bool>> BulkInsert(List<DocumentFile> listDocumentFile);
        public Task<IResult<bool>> BulkUpdate(List<DocumentFile> listDocumentFile);
        public IResults<DocumentFile> GetAllData();
        public Task<IResult<bool>> Delete(Guid id);
        public IResults<DocumentFileModel> GetAllFileById(Guid id, string tableName);
        public Task<IResult<bool>> DeleteFileById(Guid? id, string tableName);
        /// <summary>BE2.7 (Catalog 19): tải lên 1 tệp – kiểm dung lượng / đuôi / nội dung, lưu ngoài wwwroot bằng tên sinh, gắn chứng từ qua khóa lưu trữ (GAP-DOC-01). Quyền Sửa màn của chứng từ (tài liệu chung: Thêm #/adm-document).</summary>
        public Task<IResult<DocumentFileItemModel>> Upload(AppDelicare.Model.Request.Custom.DocumentFileUploadRequest request);
        /// <summary>BE2.7 (Catalog 20): danh sách tệp (lọc chữ, đuôi, loại, ngày, người tải, chứng từ). Quyền Xem màn của chứng từ (mọi tệp: Xem #/adm-document).</summary>
        public Task<IResults<DocumentFileItemModel>> GetList(AppDelicare.Model.Request.Custom.DocumentFileListRequest request);
        /// <summary>BE2.7 (Catalog 21): tải tệp – quyền Xem màn của chứng từ, ghi AuditChange "Download".</summary>
        public Task<IResult<FileContentModel>> Download(Guid id);
    }
}
