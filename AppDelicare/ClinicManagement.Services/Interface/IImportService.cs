using Microsoft.AspNetCore.Http;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Request;
using AppDelicare.Model.Response;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IImportService
    {
        public Task<IResult<bool>> Save(ImportExcelRequest request);
        public UploadFileResponse UploadFileForInsertUpdate(List<IFormFile> file, string type);
        public InformationFileViewModel GetInformationFile(IFormFile file);
        public bool UploadFile(List<FileRequest> listFile, string type);
    }
}
