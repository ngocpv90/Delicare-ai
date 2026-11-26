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

        [HttpGet("GetBacklinks")]
        public Task<IActionResult> GetBacklinks([FromQuery] string? path) => Run(nameof(GetBacklinks), () => _service.GetBacklinks(path));

        /// <summary>Lịch sử lưu (AuditChange EntityName KnowledgeNote).</summary>
        [HttpGet("GetHistory")]
        public Task<IActionResult> GetHistory([FromQuery] string? path) => Run(nameof(GetHistory), () => _service.GetHistory(path));

        /// <summary>Lưu ghi chú (Path, Content gồm frontmatter, PrevHash = băm bản đang sửa; mới thì trống). 409 = người khác vừa sửa.</summary>
        [HttpPost("Save")]
        public Task<IActionResult> Save([FromBody] KnowledgeSaveRequest request) => Run(nameof(Save), () => _service.Save(request ?? new KnowledgeSaveRequest()));
    }
}
