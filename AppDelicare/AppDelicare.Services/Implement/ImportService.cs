using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.Response;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;
using System.Net.Http.Headers;

namespace AppDelicare.Services.Implement
{
    public class ImportService(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment) : IImportService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly string _webRootPath = Path.Combine(webHostEnvironment.WebRootPath, ConstantHelper.Import);

        public async Task<IResult<bool>> Save(ImportExcelRequest request)
        {
            var filePath = string.Empty;
            try
            {
                if (request.File == null)
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.InternalServerError),
                        ResponseMessage = ConstantHelper.Message.FileRequired,
                        Data = false
                    };
                }
                var getExtension = Path.GetExtension(ContentDispositionHeaderValue.Parse(request.File.ContentDisposition).FileName?.Trim('"'));
                if (getExtension != ConstantHelper.xlsx && getExtension != ConstantHelper.xls)
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.InternalServerError),
                        ResponseMessage = ConstantHelper.Message.FileCorrectFormat,
                        Data = false
                    };
                }

                // Rà soát BE: tên tệp tạm = Guid + đuôi – không dùng tên client (chống "../" ghi ra ngoài thư mục).
                var fileNameHas = string.Concat(Guid.NewGuid().ToString("N"), getExtension);

                // Path Folder
                if (!Directory.Exists(_webRootPath))
                {
                    Directory.CreateDirectory(_webRootPath);
                }
                var pathToSave = Path.Combine(Directory.GetCurrentDirectory(), _webRootPath);
                var fullPath = Path.Combine(pathToSave, fileNameHas);

                using (FileStream fileStream = new(fullPath, FileMode.Create))
                {
                    request.File.CopyTo(fileStream);
                }

                filePath = Path.Combine(_webRootPath, fileNameHas);
                var dataTable = DataTableHelper.ConvertExeltoDataTable(fullPath);
                var result = string.Empty;
                switch (request.TableName)
                {
                    case nameof(TestDemo):
                        var testDemo = DataTableHelper.ConvertDataTable<TestDemo>(dataTable);
                        if (testDemo.Count == 0)
                        {
                            return new Result<bool>
                            {
                                ResponseCode = Convert.ToInt32(HttpStatusCode.InternalServerError),
                                ResponseMessage = ConstantHelper.Message.ImportFileNull,
                                Data = false
                            };
                        }
                        await ImportTestDemo(testDemo);
                        break;
                    default:
                        break;
                }
                if (!string.IsNullOrEmpty(result))
                {
                    DeleteFile(filePath);
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.InternalServerError),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                DeleteFile(filePath); // Rà soát BE: tệp Excel tạm không để lại trong wwwroot sau khi nhập.
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.ImportSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                DeleteFile(filePath);
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                throw;
            }
        }

        private async Task ImportTestDemo(List<TestDemo> listData)
        {
            if (listData.Count != 0)
            {
                foreach (var item in listData)
                {
                    _unitOfWork.TestDemoRepository.Add(item);
                }
                await _unitOfWork.TestDemoRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            }
        }

        private static void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private const long MaxImageBytes = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

        public InformationFileViewModel GetInformationFile(IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                return new();
            }
            else
            {
                // Rà soát BE: tên lưu trên đĩa = Guid + đuôi (chữ thường) – KHÔNG dùng tên client (chống "../" ghi ra ngoài thư mục).
                var getExtension = Path.GetExtension(Path.GetFileName(file.FileName ?? string.Empty)).ToLowerInvariant();
                var fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(file.FileName ?? string.Empty));
                var fileNameHas = string.Concat(Guid.NewGuid().ToString("N"), getExtension);
                var fileSize = Convert.ToInt32(file?.Length ?? 0);
                return new()
                {
                    FileName = fileName,
                    FileSize = fileSize,
                    FileType = getExtension,
                    FileNameHas = fileNameHas
                };
            }
        }

        public bool UploadFile(List<FileRequest> listFile, string type)
        {
            try
            {
                if (listFile.Count == 0)
                {
                    return false;
                }
                if (!Directory.Exists(_webRootPath))
                {
                    Directory.CreateDirectory(_webRootPath);
                }
                var url = string.Empty;
                switch (type)
                {
                    case ConstantHelper.FolderName.PERSON_IMAGE:
                        url = Path.Combine(_webRootPath, ConstantHelper.FolderName.PERSON_IMAGE);
                        break;
                    case ConstantHelper.FolderName.TEST_DEMO_UPLOAD:
                        url = Path.Combine(_webRootPath, ConstantHelper.FolderName.TEST_DEMO_UPLOAD);
                        break;
                    default:
                        return false; // Rà soát BE: loại thư mục lạ → không ghi (trước đây ghi vào thư mục chạy app).
                }
                // Rà soát BE: thư mục nằm trong wwwroot (phục vụ tĩnh cùng domain) → chỉ nhận ảnh PNG / JPEG ≤ 5 MB, kiểm byte đầu tệp
                // (chống tải .html / .svg / .js → XSS lưu trữ), tên tệp không chứa đường dẫn, đường dẫn cuối phải nằm trong thư mục đích.
                string root = Path.GetFullPath(url);
                foreach (var item in listFile)
                {
                    if (string.IsNullOrEmpty(item.FileName) || item.File == null || item.File.Length <= 0 || item.File.Length > MaxImageBytes)
                    {
                        return false;
                    }
                    string name = Path.GetFileName(item.FileName);
                    string ext = Path.GetExtension(name).ToLowerInvariant();
                    if (name != item.FileName || !AllowedImageExtensions.Contains(ext))
                    {
                        return false;
                    }
                    byte[] content;
                    using (var ms = new MemoryStream())
                    {
                        item.File.CopyTo(ms);
                        content = ms.ToArray();
                    }
                    if (!DocumentStorageRules.ContentMatches(ext, content))
                    {
                        return false;
                    }
                    var fullPath = Path.GetFullPath(Path.Combine(root, name));
                    if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    Directory.CreateDirectory(root);
                    File.WriteAllBytes(fullPath, content);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UploadFile: ", ex.ToString()));
                return false;
            }
        }

        public UploadFileResponse UploadFileForInsertUpdate(List<IFormFile> files, string type)
        {
            List<FileRequest> listFileUpload = [];
            List<InformationFileViewModel> listFile = [];
            foreach (var item in files)
            {
                var fileInfo = GetInformationFile(item);
                listFile.Add(new() { FileName = fileInfo.FileName, FileSize = fileInfo.FileSize, FileType = fileInfo.FileType, FileNameHas = fileInfo.FileNameHas });
                listFileUpload.Add(new() { FileName = fileInfo.FileNameHas ?? string.Empty, File = item });
            }
            return new()
            {
                Result = UploadFile(listFileUpload, type),
                ListFile = listFile
            };
        }
    }
}
