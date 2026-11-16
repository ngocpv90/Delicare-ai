using System.ComponentModel.DataAnnotations;
using AppDelicare.Data.Interface;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.WebApi.Controllers.V1
{
    /// <summary>Một dòng lịch sử xử lý của bản ghi (tab "Lịch sử xử lý" trong drawer chi tiết).</summary>
    public sealed class AuditHistoryItem
    {
        /// <summary>Id dòng AuditChange.</summary>
        /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
        public Guid Id { get; init; }

        /// <summary>Thời điểm thao tác (giờ Việt Nam).</summary>
        /// <example>2026-11-14T18:14:32</example>
        public DateTime At { get; init; }

        /// <summary>User.Id người thao tác.</summary>
        /// <example>a0ed3907-b6ad-451e-a14f-b974ccab7160</example>
        public Guid? By { get; init; }

        /// <summary>Tên đăng nhập người thao tác (null nếu không tìm thấy).</summary>
        /// <example>admin</example>
        public string? ByUserName { get; init; }

        /// <summary>Hành động: Added / Modified (theo EF), sau này thêm TRANSITION.</summary>
        /// <example>Added</example>
        public string? Action { get; init; }

        /// <summary>Nội dung thay đổi. Dòng đầu: người thao tác + CorrelationId (GAP-AUD-01).</summary>
        /// <example>- Actor: USER:a0ed3907-… | CorrelationId: 9f1c… &lt;br /&gt; - FieldString: '[DEMO]' &lt;br /&gt;</example>
        public string? Changes { get; init; }
    }

    /// <summary>Nhật ký thay đổi (Spec 2.2 – <c>/api/v1/audit/{table}/{id}/history</c>).</summary>
    [Route(RoutePrefix + "/audit")]
    public sealed class AuditController(IUnitOfWork unitOfWork) : ApiControllerBase
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        /// <summary>Lịch sử xử lý của một bản ghi, mới nhất trước.</summary>
        /// <remarks>
        /// Ví dụ: <c>GET /api/v1/audit/TestDemo/1aa5ca26-4dde-4b0e-93ed-08df42be31a5/history?take=20</c>.
        /// <para><c>table</c> là tên entity được ghi trong AuditChange.EntityName (tên class C#, vd. <c>ProductionBatch</c>).</para>
        /// Hiện mới gộp AuditChange; ApprovalDecision / ComplaintHistory sẽ gộp khi có module tương ứng.
        /// </remarks>
        /// <param name="table">Tên entity: chữ, số, gạch dưới; tối đa 128 ký tự.</param>
        /// <param name="id">Id bản ghi.</param>
        /// <param name="take">Số dòng tối đa (1–200, mặc định 50).</param>
        /// <param name="cancellationToken">Hủy khi client ngắt kết nối.</param>
        /// <response code="200">Envelope, <c>data</c> là danh sách lịch sử (có thể rỗng).</response>
        /// <response code="400">VALIDATION_FAILED – tham số sai.</response>
        [HttpGet("{table}/{id:guid}/history")]
        [Permission("adm-audit", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<AuditHistoryItem>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHistory(
            [FromRoute, Required, RegularExpression("^[A-Za-z_][A-Za-z0-9_]{0,127}$", ErrorMessage = "Tên bảng chỉ gồm chữ, số, gạch dưới (tối đa 128 ký tự).")] string table,
            [FromRoute] Guid id,
            [FromQuery, Range(1, 200, ErrorMessage = "take phải từ 1 đến 200.")] int take = 50,
            CancellationToken cancellationToken = default)
        {
            var rows = await _unitOfWork.AuditChangeRepository.GetAllNoneDeleted()
                .AsNoTracking()
                // GetAllNoneDeleted() hiện KHÔNG lọc IsDeleted (CODE_AUDIT A18) → lọc tường minh.
                .Where(x => !x.IsDeleted && x.EntityName == table && x.IdChange == id)
                .OrderByDescending(x => x.CreatedDate)
                .Take(take)
                .Select(x => new { x.Id, x.CreatedDate, x.CreatedBy, x.Action, x.Changes })
                .ToListAsync(cancellationToken);

            var userIds = rows.Where(r => r.CreatedBy.HasValue).Select(r => r.CreatedBy!.Value).Distinct().ToList();
            var userNames = userIds.Count == 0
                ? new Dictionary<Guid, string?>()
                : await _unitOfWork.UserRepository.GetAll().AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.UserName, cancellationToken);

            var items = rows.Select(r => new AuditHistoryItem
            {
                Id = r.Id,
                At = r.CreatedDate,
                By = r.CreatedBy,
                ByUserName = r.CreatedBy.HasValue && userNames.TryGetValue(r.CreatedBy.Value, out var name) ? name : null,
                Action = r.Action,
                Changes = r.Changes,
            }).ToList();

            return Ok(items);
        }
    }
}
