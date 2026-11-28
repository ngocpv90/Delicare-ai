using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed class KnowledgeSyncNoteModel
    {
        public string Path { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        /// <summary>Ghi chú tự sinh (source: delicare-core) – luôn chỉ đọc kể cả ở thư mục hai chiều.</summary>
        public bool ReadOnly { get; set; }
    }

    public sealed class KnowledgeSyncManifestModel
    {
        public DateTime At { get; set; }
        /// <summary>Thư mục được đẩy sửa từ máy lên (qua Knowledge/Save, kiểm băm + quyền Sửa). Rỗng = mọi thư mục một chiều.</summary>
        public List<string> TwoWayFolders { get; set; } = [];
        public List<KnowledgeSyncNoteModel> ListNote { get; set; } = [];
    }

    /// <summary>
    /// AI-18 (D-133): đồng bộ kho tri thức xuống Obsidian máy cá nhân (tools/kb-sync/kb-sync.mjs, không thư viện mới). Máy chủ trả danh sách ghi chú người dùng được xem
    /// – KHÔNG BAO GIỜ thư mục BOD / ghi chú confidentiality BOD, kể cả người được cấp BOD (tài liệu Ban GĐ không rời máy chủ) – kèm băm để script chỉ tải tệp đổi.
    /// Hai chiều chỉ thư mục 80 Đào tạo khi <c>Knowledge:SyncTwoWayTraining = true</c> VÀ người dùng có quyền Sửa thư mục 80; ghi vẫn qua Knowledge/Save (quét dữ liệu cá nhân, khóa băm, AuditChange).
    /// Mỗi lần lấy danh sách ghi 1 dòng AuditChange <c>KnowledgeSync</c> (số ghi chú) để biết ai đã tải kho về máy.
    /// </summary>
    public partial class KnowledgeService
    {
        public const string SyncEntity = "KnowledgeSync";
        public const string TrainingFolder = "80";

        public async Task<IResult<KnowledgeSyncManifestModel>> GetSyncManifest()
        {
            var caller = WebCaller();
            if (caller.AgentCode != null)
            {
                return Status<KnowledgeSyncManifestModel>(403, "Agent không đồng bộ kho về máy");
            }
            var rights = await RightsAsync(caller);
            var notes = All().Where(n => n.Folder.Length > 0 && n.Folder != KnowledgeVault.Bod && !n.IsBod && rights.NoteVisible(n))
                .Select(n => new KnowledgeSyncNoteModel { Path = n.Path, Folder = n.Folder, Hash = n.Hash, ReadOnly = n.Generated })
                .OrderBy(n => n.Path, StringComparer.Ordinal).ToList();
            var m = new KnowledgeSyncManifestModel
            {
                At = clock.Now, ListNote = notes,
                TwoWayFolders = _o.SyncTwoWayTraining && rights.CanEdit(TrainingFolder) ? [TrainingFolder] : [],
            };
            unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = caller.UserId, EntityName = SyncEntity, Action = "Manifest",
                Changes = JsonConvert.SerializeObject(new { notes = notes.Count, folders = notes.Select(n => n.Folder).Distinct().OrderBy(x => x), twoWay = m.TwoWayFolders }),
                CreatedDate = clock.Now, CreatedBy = caller.UserId, IsDeleted = false,
            });
            await unitOfWork.DbContext.SaveChangesAsync();
            return Result<KnowledgeSyncManifestModel>.Success(m, "OK");
        }
    }
}
