using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// AI-5 (D-133) – Kho tri thức. Quyền theo TỪNG THƯ MỤC (mục menu <c>knowledge?folder=&lt;mã&gt;</c>: Xem = đọc, Sửa = soạn; BOD chỉ người được cấp rõ)
    /// kiểm trong <see cref="IKnowledgeService"/> – controller chỉ cần đăng nhập. Người không có thư mục nào: cây rỗng, đọc / ghi 403 / 404.
    /// </summary>
    [Security.SkipPermission]
    public class KnowledgeController(IKnowledgeService service) : BaseController
    {
        private readonly IKnowledgeService _service = service;

        private async Task<IActionResult> Run<T>(string name, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Cây thư mục + ghi chú người dùng được xem (kèm quyền soạn từng thư mục).</summary>
        [HttpGet("GetTree")]
        public Task<IActionResult> GetTree() => Run(nameof(GetTree), _service.GetTree);

        /// <summary>1 ghi chú: frontmatter, nội dung (ngưỡng {{BusinessRules:…}} đã thay), băm để sửa, liên kết ngược.</summary>
        [HttpGet("GetNote")]
        public Task<IActionResult> GetNote([FromQuery] string? path) => Run(nameof(GetNote), () => _service.GetNote(path));

        /// <summary>Tìm toàn văn (không dấu) – Keyword, Folder, IncludeHistory.</summary>
        [HttpGet("Search")]
        public Task<IActionResult> Search([FromQuery] KnowledgeSearchRequest request) => Run(nameof(Search), () => _service.Search(request ?? new KnowledgeSearchRequest()));

        /// <summary>AI-17 (D-133): tab Tri thức trên trang chi tiết – ghi chú gắn mã của bản ghi (refs / nhắc trong nội dung), theo quyền thư mục của người xem.</summary>
        /// <summary>AI-18 (D-133): danh sách ghi chú để đồng bộ Obsidian máy cá nhân (tools/kb-sync/kb-sync.mjs) – theo quyền thư mục, không BOD.</summary>
        [HttpGet("GetSyncManifest")]
        public Task<IActionResult> GetSyncManifest() => Run(nameof(GetSyncManifest), _service.GetSyncManifest);

        [HttpGet("GetByEntity")]
        public Task<IActionResult> GetByEntity([FromQuery] string? type, [FromQuery] Guid id) => Run(nameof(GetByEntity), () => _service.GetByEntity(type, id));

        [HttpGet("GetBacklinks")]
        public Task<IActionResult> GetBacklinks([FromQuery] string? path) => Run(nameof(GetBacklinks), () => _service.GetBacklinks(path));

        /// <summary>Lịch sử lưu (AuditChange EntityName KnowledgeNote).</summary>
        [HttpGet("GetHistory")]
        public Task<IActionResult> GetHistory([FromQuery] string? path) => Run(nameof(GetHistory), () => _service.GetHistory(path));

        /// <summary>Lưu ghi chú (Path, Content gồm frontmatter, PrevHash = băm bản đang sửa; mới thì trống). 409 = người khác vừa sửa.</summary>
        [HttpPost("Save")]
        public Task<IActionResult> Save([FromBody] KnowledgeSaveRequest request) => Run(nameof(Save), () => _service.Save(request ?? new KnowledgeSaveRequest()));

        /// <summary>AI-12: gửi duyệt SOP / hướng dẫn / quyết định (Nháp → Chờ duyệt, mục Phê duyệt › Duyệt SOP). Quyền Sửa thư mục kiểm trong service.</summary>
        [HttpPost("SubmitSop")]
        public Task<IActionResult> SubmitSop([FromBody] KnowledgePathRequest request) => Run(nameof(SubmitSop), () => _service.SubmitSop(request ?? new KnowledgePathRequest()));

        /// <summary>AI-12: phiên bản mới (Nháp, cùng id) của ghi chú đang Hiệu lực.</summary>
        [HttpPost("NewVersion")]
        public Task<IActionResult> NewVersion([FromBody] KnowledgePathRequest request) => Run(nameof(NewVersion), () => _service.NewVersion(request ?? new KnowledgePathRequest()));

        /// <summary>AI-22.2 (D-141): tải file gốc (pdf / docx / xlsx / jpg / png ≤ Knowledge:MaxFileMb) + phiếu mô tả vào 1 vùng – quyền Sửa vùng kiểm trong service.</summary>
        [HttpPost("UploadFile")]
        [RequestSizeLimit(60_000_000)]
        public Task<IActionResult> UploadFile([FromForm] KnowledgeUploadRequest request, IFormFile? file) => Run(nameof(UploadFile), async () =>
        {
            if (file == null || file.Length == 0)
            {
                return (AppDelicare.Data.Interface.IResult<KnowledgeSaveResultModel>)AppDelicare.Data.Repositories.Result<KnowledgeSaveResultModel>.Fail("Chọn tệp cần tải lên");
            }
            using var s = file.OpenReadStream();
            return await _service.UploadFile(request ?? new KnowledgeUploadRequest(), file.FileName, s, file.Length);
        });

        /// <summary>AI-22.2: tải file gốc của tài liệu – chỉ qua API, quyền Xuất vùng; vùng hạn chế ghi nhật ký truy cập.</summary>
        [HttpGet("DownloadFile")]
        public async Task<IActionResult> DownloadFile([FromQuery] string? path)
        {
            try
            {
                var r = await _service.DownloadFile(path);
                return r.ResponseCode == 200 && r.Data != null ? File(r.Data.Content, r.Data.ContentType, r.Data.FileName) : Ok(r);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] DownloadFile: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>AI-22.2: chuyển Lưu trữ (thay xóa) – quyền Xóa vùng.</summary>
        [HttpPost("Archive")]
        public Task<IActionResult> Archive([FromBody] KnowledgePathRequest request) => Run(nameof(Archive), () => _service.Archive(request ?? new KnowledgePathRequest()));

        /// <summary>AI-22.2: nhật ký xem / tải 1 tài liệu vùng hạn chế (người có quyền Sửa vùng).</summary>
        [HttpGet("GetAccessLog")]
        public Task<IActionResult> GetAccessLog([FromQuery] string? path) => Run(nameof(GetAccessLog), () => _service.GetAccessLog(path));

        /// <summary>AI-12: nhập Word .docx (/ .md / .txt) thành ghi chú Nháp trong thư mục (PDF: GAP-KB-PDF). Tối đa 10 MB.</summary>
        [HttpPost("Import")]
        [RequestSizeLimit(10_000_000)]
        public Task<IActionResult> Import([FromForm] string? folder, [FromForm] string? type, IFormFile? file) => Run(nameof(Import), async () =>
        {
            if (file == null || file.Length == 0)
            {
                return (AppDelicare.Data.Interface.IResult<KnowledgeSaveResultModel>)AppDelicare.Data.Repositories.Result<KnowledgeSaveResultModel>.Fail("Chọn tệp Word (.docx) để nhập");
            }
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;
            return await _service.Import(folder, type, file.FileName, ms);
        });
    }
}
