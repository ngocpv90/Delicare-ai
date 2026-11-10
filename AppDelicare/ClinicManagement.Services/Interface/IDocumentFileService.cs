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
    }
}
