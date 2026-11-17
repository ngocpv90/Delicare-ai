using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class DocumentFileService(IUnitOfWork unitOfWork, IOptions<AppSettingHelper> options
        , IOptions<AppDelicare.Model.Option.DocumentStorageOptions> storage, AppDelicare.Data.Share.ICurrentActor currentActor, IPermissionService permissionService
        , AppDelicare.Data.Share.IClock clock) : IDocumentFileService
    {
        /// <summary>BE2.7: tài liệu chung (không gắn chứng từ) theo màn #/adm-document (ScreenMap DocumentFile).</summary>
        public const string DocumentScreen = "adm-document";

        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly AppSettingHelper _appSettingHelper = options.Value;
        private readonly AppDelicare.Model.Option.DocumentStorageOptions _storage = storage.Value;
        private readonly AppDelicare.Data.Share.ICurrentActor _currentActor = currentActor;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly AppDelicare.Data.Share.IClock _clock = clock;

        public async Task<IResult<bool>> Delete(Guid id)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                if (string.IsNullOrEmpty(id.ToString()))
                {
                    result = ConstantHelper.Message.RecordNotFoundMessage;
                }
                var delete = _unitOfWork.DocumentFileRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                if (delete == null)
                {
                    result = ConstantHelper.Message.RecordNotFoundMessage;
                }
                if (!string.IsNullOrEmpty(result))
                {
                    _unitOfWork.CommitTransaction();
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                if (delete != null)
                {
                    _unitOfWork.DocumentFileRepository.Update(delete);
                    await _unitOfWork.DocumentFileRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                }
                _unitOfWork.CommitTransaction();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.DeleteSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Delete: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public IResults<DocumentFile> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DocumentFileRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DocumentFile>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> BulkInsert(List<DocumentFile> listDocumentFile)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (listDocumentFile.Count != 0)
                {
                    foreach (var item in listDocumentFile)
                    {
                        _unitOfWork.DocumentFileRepository.Add(item);
                    }
                    await _unitOfWork.DocumentFileRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                }
                _unitOfWork.CommitTransaction();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.TypeSave.Delete,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " BulkInsert: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> BulkUpdate(List<DocumentFile> listDocumentFile)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                if (listDocumentFile.Count != 0)
                {
                    foreach (var item in listDocumentFile)
                    {
                        _unitOfWork.DocumentFileRepository.Update(item);
                    }
                    await _unitOfWork.DocumentFileRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.TypeSave.Delete,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " BulkUpdate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> DeleteFileById(Guid? id, string tableName)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var list = GetAllData().ListData;
                var folderName = string.Empty;
                switch (tableName)
                {
                    case nameof(TestDemo):
                        list = list.Where(x => x.TestDemoId == id).ToList();
                        break;
                    default:
                        break;
                }
                if (list.Count != 0)
                {
                    foreach (var item in list)
                    {
                        _unitOfWork.DocumentFileRepository.Update(item);
                    }
                    await _unitOfWork.DocumentFileRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                }
                _unitOfWork.CommitTransaction();
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.TypeSave.Delete,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " DeleteFileById: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public IResults<DocumentFileModel> GetAllFileById(Guid id, string tableName)
        {
            try
            {
                var list = GetAllData().ListData.OrderByDescending(x => x.CreatedDate).ToList();
                var folderName = string.Empty;
                switch (tableName)
                {
                    case nameof(TestDemo):
                        folderName = ConstantHelper.FolderName.TEST_DEMO_UPLOAD;
                        list = list.Where(x => x.TestDemoId == id).ToList();
                        break;
                    default:
                        break;
                }
                var listData = list.Select(x => new DocumentFileModel
                {
                    Id = x.Id,
                    TestDemoId = x.TestDemoId,
                    FileName = x.FileName,
                    FileSize = x.FileSize,
                    FileType = x.FileType,
                    FileNameHas = x.FileNameHas,
                    FileNameUrl = !string.IsNullOrEmpty(x.FileNameHas) ? Path.Combine(_appSettingHelper.DomainFile ?? string.Empty, ConstantHelper.Import, folderName, x.FileNameHas) : string.Empty,
                }).ToList();
                return new Results<DocumentFileModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = listData,
                    TotalRecords = listData.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllFileById: ", ex.ToString()));
                throw;
            }
        }

        // ---------------------------------------------------------------- BE2.7 – tệp đính kèm (spec 2.4, Catalog 19–21)

        /// <summary>Thư mục gốc lưu tệp (Startup đã đổi đường dẫn tương đối thành tuyệt đối theo ContentRoot).</summary>
        private string Root => Path.GetFullPath(string.IsNullOrWhiteSpace(_storage.RootPath) ? "App_Data/DocumentFiles" : _storage.RootPath);

        /// <summary>Đuôi được nhận = cấu hình ∩ đuôi kiểm được nội dung, trừ tệp chạy được.</summary>
        private List<string> AllowedExtensions => _storage.AllowedExtensions
            .Select(e => (e.StartsWith('.') ? e : "." + e).ToLowerInvariant())
            .Where(e => !DocumentStorageRules.NeverAllowed.Contains(e) && DocumentStorageRules.ContentTypes.ContainsKey(e))
            .Distinct().ToList();

        private static Result<T> Forbid<T>(string message) => new() { ResponseCode = CommonAccess.Forbidden, ResponseMessage = message };

        /// <summary>Màn hình chịu quyền của tệp: màn của chứng từ, hoặc #/adm-document với tài liệu chung.</summary>
        private static string ScreenOf(DocumentStorageRules.StorageKey key)
            => key.Resource != null && CommonRegistry.Find(key.Resource) is { } r ? r.ScreenId : DocumentScreen;

        private static DocumentFileItemModel Item(DocumentFile f, IReadOnlyDictionary<Guid, string>? names = null, bool duplicate = false)
        {
            var key = DocumentStorageRules.Parse(f.FileNameHas);
            string ext = f.FileType ?? key?.Extension ?? string.Empty;
            return new DocumentFileItemModel
            {
                Id = f.Id,
                FileName = (f.FileName ?? string.Empty) + ext,
                FileType = ext,
                ContentType = DocumentStorageRules.ContentTypes.GetValueOrDefault(ext, "application/octet-stream"),
                FileSize = f.FileSize,
                Category = key?.Category,
                Resource = key?.Resource,
                RecordId = key?.RecordId,
                Sha256 = key?.Sha256,
                CreatedDate = f.CreatedDate,
                CreatedBy = f.CreatedBy,
                CreatedByName = f.CreatedBy is { } by && names != null ? names.GetValueOrDefault(by) : null,
                Url = key == null ? null : "api/DocumentFile/Download?id=" + f.Id,
                IsDuplicate = duplicate,
            };
        }

        public async Task<IResult<DocumentFileItemModel>> Upload(AppDelicare.Model.Request.Custom.DocumentFileUploadRequest request)
        {
            string? written = null;
            try
            {
                var file = request.File;
                if (file == null || file.Length <= 0)
                {
                    return Result<DocumentFileItemModel>.Fail(ConstantHelper.Message.FileRequired);
                }
                long max = Math.Max(1, _storage.MaxFileSizeMb) * 1024L * 1024L;
                if (file.Length > max)
                {
                    return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.FileTooLarge, _storage.MaxFileSizeMb));
                }
                string category = string.IsNullOrWhiteSpace(request.Category) ? "other" : request.Category.Trim().ToLowerInvariant();
                if (!_storage.Categories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)) || !category.All(char.IsAsciiLetterLower))
                {
                    return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.FileCategoryInvalid, request.Category, string.Join(", ", _storage.Categories)));
                }
                string original = DocumentStorageRules.SafeFileName(file.FileName);
                string ext = Path.GetExtension(original).ToLowerInvariant();
                var allowed = AllowedExtensions;
                if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                {
                    return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.FileExtensionNotAllowed, original, string.Join(", ", allowed)));
                }
                byte[] content;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    content = ms.ToArray();
                }
                if (content.Length > max)
                {
                    return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.FileTooLarge, _storage.MaxFileSizeMb));
                }
                string expectedType = DocumentStorageRules.ContentTypes[ext];
                string declared = (file.ContentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
                bool declaredOk = declared.Length == 0 || declared == "application/octet-stream" || declared == expectedType
                    || (expectedType == "image/jpeg" && declared is "image/jpg" or "image/pjpeg");
                if (!declaredOk || !DocumentStorageRules.ContentMatches(ext, content))
                {
                    return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.FileContentMismatch, ext));
                }

                // Chứng từ chủ: danh sách trắng + bản ghi còn + quyền Sửa màn đó; tài liệu chung: quyền Thêm #/adm-document.
                var db = _unitOfWork.DbContext;
                CommonResource? owner = null;
                if (!string.IsNullOrWhiteSpace(request.Resource) || request.RecordId != null)
                {
                    if (string.IsNullOrWhiteSpace(request.Resource) || request.RecordId == null || request.RecordId == Guid.Empty)
                    {
                        return Result<DocumentFileItemModel>.Fail(ConstantHelper.Message.FileRecordRequired);
                    }
                    owner = CommonRegistry.Find(request.Resource);
                    if (owner == null || !owner.Attachable)
                    {
                        return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.CommonResourceNotSupported, request.Resource));
                    }
                    if (await CommonAccess.DenyAsync(_permissionService, _currentActor, owner.ScreenId, PermissionAction.Edit) is { } deny)
                    {
                        return Forbid<DocumentFileItemModel>(deny);
                    }
                    if (!await owner.ExistsAsync(db, request.RecordId.Value))
                    {
                        return Result<DocumentFileItemModel>.Fail(string.Format(ConstantHelper.Message.CommonRecordNotFound, owner.Title));
                    }
                }
                else if (await CommonAccess.DenyAsync(_permissionService, _currentActor, DocumentScreen, PermissionAction.Add) is { } denyGeneral)
                {
                    return Forbid<DocumentFileItemModel>(denyGeneral);
                }

                string sha = DocumentStorageRules.Sha256Hex(content);
                string key = DocumentStorageRules.BuildKey(owner?.Key, request.RecordId, category, sha, ext);
                // Cùng nội dung, cùng chứng từ, cùng loại → trả tệp đã có (spec: FileNameHas để phát hiện trùng).
                var existing = await db.DocumentFile.AsNoTracking().FirstOrDefaultAsync(f => f.FileNameHas == key && !f.IsDeleted);
                if (existing != null)
                {
                    var names = await CommonAccess.UserNamesAsync(db, [existing.CreatedBy]);
                    return Result<DocumentFileItemModel>.Success(Item(existing, names, duplicate: true), ConstantHelper.Message.SaveSuccess);
                }

                string path = DocumentStorageRules.FullPath(Root, key);
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        await fs.WriteAsync(content);
                    }
                    written = path;
                }

                var entity = new DocumentFile
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileNameWithoutExtension(original),
                    FileType = ext,
                    FileSize = content.Length,
                    FileNameHas = key,
                };
                using var tran = await _unitOfWork.OpenTransaction();
                try
                {
                    _unitOfWork.DocumentFileRepository.Add(entity);
                    await _unitOfWork.DocumentFileRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    _unitOfWork.CommitTransaction();
                }
                catch
                {
                    _unitOfWork.RollbackTransaction();
                    throw;
                }
                written = null;
                var actorNames = await CommonAccess.UserNamesAsync(db, [entity.CreatedBy]);
                return Result<DocumentFileItemModel>.Success(Item(entity, actorNames), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Upload: ", ex.ToString()));
                if (written != null && File.Exists(written))
                {
                    File.Delete(written); // ghi DB lỗi → không để tệp mồ côi
                }
                throw;
            }
        }

        public async Task<IResults<DocumentFileItemModel>> GetList(AppDelicare.Model.Request.Custom.DocumentFileListRequest request)
        {
            try
            {
                string? prefix = null;
                if (!string.IsNullOrWhiteSpace(request.Resource) || request.RecordId != null)
                {
                    var owner = CommonRegistry.Find(request.Resource);
                    if (owner == null || !owner.Attachable || request.RecordId == null)
                    {
                        return Results<DocumentFileItemModel>.Fail(owner == null || !owner.Attachable
                            ? string.Format(ConstantHelper.Message.CommonResourceNotSupported, request.Resource) : ConstantHelper.Message.FileRecordRequired);
                    }
                    if (await CommonAccess.DenyAsync(_permissionService, _currentActor, owner.ScreenId, PermissionAction.View) is { } deny)
                    {
                        return new Results<DocumentFileItemModel>([], 0) { ResponseCode = CommonAccess.Forbidden, ResponseMessage = deny };
                    }
                    prefix = DocumentStorageRules.OwnerPrefix(owner.Key, request.RecordId.Value);
                }
                else if (await CommonAccess.DenyAsync(_permissionService, _currentActor, DocumentScreen, PermissionAction.View) is { } denyAll)
                {
                    return new Results<DocumentFileItemModel>([], 0) { ResponseCode = CommonAccess.Forbidden, ResponseMessage = denyAll };
                }

                var db = _unitOfWork.DbContext;
                IQueryable<DocumentFile> query = db.DocumentFile.AsNoTracking().Where(f => !f.IsDeleted);
                if (prefix != null)
                {
                    query = query.Where(f => f.FileNameHas != null && f.FileNameHas.StartsWith(prefix));
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string text = request.SearchText.Trim();
                    query = query.Where(f => f.FileName != null && f.FileName.Contains(text));
                }
                if (!string.IsNullOrWhiteSpace(request.FileType))
                {
                    string type = "." + request.FileType.Trim().TrimStart('.').ToLowerInvariant();
                    query = query.Where(f => f.FileType == type);
                }
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    string segment = "/" + request.Category.Trim().ToLowerInvariant() + "/";
                    query = query.Where(f => f.FileNameHas != null && f.FileNameHas.Contains(segment));
                }
                if (request.FromDate is { } from)
                {
                    DateTime start = from.Date;
                    query = query.Where(f => f.CreatedDate >= start);
                }
                if (request.ToDate is { } to)
                {
                    DateTime end = to.Date.AddDays(1);
                    query = query.Where(f => f.CreatedDate < end);
                }
                if (request.CreatedBy is { } createdBy)
                {
                    query = query.Where(f => f.CreatedBy == createdBy);
                }
                int total = await query.CountAsync();
                int page = Math.Max(1, request.Page);
                int size = Math.Clamp(request.PageSize, 1, 200);
                var rows = await query.OrderByDescending(f => f.CreatedDate).ThenBy(f => f.Id).Skip((page - 1) * size).Take(size).ToListAsync();
                var names = await CommonAccess.UserNamesAsync(db, rows.Select(r => r.CreatedBy));
                return new Results<DocumentFileItemModel>([.. rows.Select(r => Item(r, names))], total)
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<FileContentModel>> Download(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var file = await db.DocumentFile.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
                if (file == null)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.FileNotFound);
                }
                var key = DocumentStorageRules.Parse(file.FileNameHas);
                if (key == null)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.FileLegacyNotSupported);
                }
                if (await CommonAccess.DenyAsync(_permissionService, _currentActor, ScreenOf(key), PermissionAction.View) is { } deny)
                {
                    return Forbid<FileContentModel>(deny);
                }
                string path = DocumentStorageRules.FullPath(Root, file.FileNameHas!);
                if (!File.Exists(path))
                {
                    Log.Warning("DocumentFile {Id}: không thấy tệp trên đĩa", id);
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.FileMissingOnDisk);
                }
                byte[] content = await File.ReadAllBytesAsync(path);

                // Spec 2.4: ghi audit lượt tải. AuditChange chỉ ghi thêm; SaveChanges thường (không typeSave) → không sinh audit của chính dòng audit.
                var actor = _currentActor.RequireActor();
                db.AuditChange.Add(new AuditChange
                {
                    Id = Guid.NewGuid(),
                    IdChange = file.Id,
                    EntityName = nameof(DocumentFile),
                    Action = "Download",
                    CreatedDate = _clock.Now,
                    CreatedBy = actor.UserId,
                    Changes = $"- Actor: {actor} | CorrelationId: {_currentActor.CorrelationId} {ConstantHelper.Break}" + Environment.NewLine
                        + $"- FileName: '{file.FileName}{file.FileType}' {ConstantHelper.Break}" + Environment.NewLine,
                    IsDeleted = false,
                });
                await db.SaveChangesAsync();

                return Result<FileContentModel>.Success(new FileContentModel
                {
                    Content = content,
                    ContentType = DocumentStorageRules.ContentTypes.GetValueOrDefault(key.Extension, "application/octet-stream"),
                    FileName = DocumentStorageRules.SafeFileName((file.FileName ?? "tep") + (file.FileType ?? key.Extension)),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Download: ", ex.ToString()));
                throw;
            }
        }
    }
}
