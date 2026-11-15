using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Data.Share;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.WebApi.Controllers.V1.Hr
{
    /// <summary>Ca làm việc (bảng <c>Shift</c>) – trả về.</summary>
    public sealed class ShiftDto
    {
        /// <example>6b1f3c2e-9a7d-4e21-8f00-1c2d3e4f5a6b</example>
        public Guid Id { get; init; }
        /// <summary>Công ty (Company.Id).</summary>
        public Guid? CompanyId { get; init; }
        /// <summary>Mã ca.</summary>
        /// <example>TRUA</example>
        public string? Code { get; init; }
        /// <summary>Tên ca.</summary>
        /// <example>Ca trưa</example>
        public string? Name { get; init; }
        /// <summary>Giờ bắt đầu (HH:mm:ss).</summary>
        /// <example>10:00:00</example>
        public TimeSpan? StartTime { get; init; }
        /// <summary>Giờ kết thúc (HH:mm:ss). Nhỏ hơn giờ bắt đầu = ca qua đêm.</summary>
        /// <example>14:00:00</example>
        public TimeSpan? EndTime { get; init; }
        /// <summary>Ca qua đêm (EndTime &lt; StartTime).</summary>
        /// <example>false</example>
        public bool IsOvernight { get; init; }
        /// <summary>Trạng thái: Active / Inactive (PROPOSED – BA/PO CONFIRM).</summary>
        /// <example>Active</example>
        public string? Status { get; init; }
        /// <summary>Ghi chú.</summary>
        public string? Note { get; init; }
        /// <summary>ETag hiện tại – gửi lại trong header <c>If-Match</c> khi PUT.</summary>
        /// <example>"638672048720000000"</example>
        public string ETag { get; init; } = string.Empty;
    }

    /// <summary>Ca làm việc – dữ liệu tạo / sửa.</summary>
    public sealed class ShiftUpsertRequest
    {
        /// <summary>Công ty (Company.Id), không bắt buộc. Có gửi thì phải tồn tại (422 REF_NOT_FOUND).</summary>
        public Guid? CompanyId { get; init; }

        /// <summary>Mã ca, duy nhất trong các ca chưa xóa (409 DUPLICATE).</summary>
        /// <example>TRUA</example>
        [Required(ErrorMessage = "Mã ca là bắt buộc."), StringLength(255, ErrorMessage = "Mã ca tối đa 255 ký tự.")]
        public string? Code { get; init; }

        /// <summary>Tên ca.</summary>
        /// <example>Ca trưa</example>
        [Required(ErrorMessage = "Tên ca là bắt buộc."), StringLength(255, ErrorMessage = "Tên ca tối đa 255 ký tự.")]
        public string? Name { get; init; }

        /// <summary>Giờ bắt đầu, dạng HH:mm:ss.</summary>
        /// <example>10:00:00</example>
        [Required(ErrorMessage = "Giờ bắt đầu là bắt buộc.")]
        public TimeSpan? StartTime { get; init; }

        /// <summary>Giờ kết thúc, dạng HH:mm:ss. Cho phép nhỏ hơn giờ bắt đầu (ca qua đêm, Spec 4.7).</summary>
        /// <example>14:00:00</example>
        [Required(ErrorMessage = "Giờ kết thúc là bắt buộc.")]
        public TimeSpan? EndTime { get; init; }

        /// <summary>Trạng thái: Active / Inactive. Mặc định Active.</summary>
        /// <example>Active</example>
        [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Trạng thái chỉ nhận Active hoặc Inactive.")]
        public string? Status { get; init; }

        /// <summary>Ghi chú.</summary>
        [StringLength(4000)]
        public string? Note { get; init; }
    }

    /// <summary>Lọc danh sách ca.</summary>
    public sealed class ShiftQuery : PageQuery
    {
        /// <summary>Lọc theo trạng thái.</summary>
        /// <example>Active</example>
        [FromQuery(Name = "status")]
        public string? Status { get; set; }

        /// <summary>Lọc theo công ty.</summary>
        [FromQuery(Name = "companyId")]
        public Guid? CompanyId { get; set; }
    }

    /// <summary>
    /// Ca làm việc – <c>/api/v1/hr/shifts</c> (Spec 4.7). Endpoint mẫu của khung v1 (T03):
    /// envelope, mã lỗi Phụ lục A, phân trang / sort, ETag + If-Match, Idempotency-Key.
    /// Phân quyền <c>[Permission]</c> gắn ở T07. Logic sẽ chuyển sang AppDelicare.Application (T10).
    /// </summary>
    [Route(RoutePrefix + "/hr/shifts")]
    public sealed class ShiftsController(DatabaseContext db, IReferenceGuard referenceGuard) : ApiControllerBase
    {
        public const string StatusActive = "Active";
        /// <summary>Màn hình mockup – MetaLink #/hr-shift.</summary>
        public const string Screen = "hr-shift";

        private static readonly Dictionary<string, Expression<Func<Shift, object?>>> SortFields = new()
        {
            ["code"] = x => x.Code,
            ["name"] = x => x.Name,
            ["startTime"] = x => x.StartTime,
            ["endTime"] = x => x.EndTime,
            ["status"] = x => x.Status,
            ["createdDate"] = x => x.CreatedDate,
        };

        /// <summary>Danh sách ca (phân trang, tìm theo mã / tên, sort).</summary>
        /// <response code="200">Envelope với <c>data = { items, total, page, pageSize }</c>.</response>
        [HttpGet]
        [Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<PagedData<ShiftDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] ShiftQuery query, CancellationToken cancellationToken)
        {
            IQueryable<Shift> source = db.Shift.AsNoTracking().Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                string q = query.Q.Trim();
                source = source.Where(x => (x.Code != null && x.Code.Contains(q)) || (x.Name != null && x.Name.Contains(q)));
            }
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                source = source.Where(x => x.Status == query.Status);
            }
            if (query.CompanyId.HasValue)
            {
                source = source.Where(x => x.CompanyId == query.CompanyId);
            }

            int total = await source.CountAsync(cancellationToken);
            var rows = await source
                .ApplySort(query.Sort, SortFields, s => s.OrderBy(x => x.StartTime).ThenBy(x => x.Code))
                .Page(query)
                .ToListAsync(cancellationToken);

            return Ok(new PagedData<ShiftDto> { Items = rows.Select(ToDto).ToList(), Total = total, Page = query.Page, PageSize = query.PageSize });
        }

        /// <summary>Chi tiết một ca. Header <c>ETag</c> dùng cho PUT.</summary>
        /// <response code="200">Envelope với <c>data</c> là ca.</response>
        /// <response code="404">NOT_FOUND – không có hoặc đã xóa.</response>
        [HttpGet("{id:guid}")]
        [Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<ShiftDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        {
            Shift entity = await FindAsync(id, tracking: false, cancellationToken);
            var dto = ToDto(entity);
            SetETag(dto.ETag);
            return Ok(dto);
        }

        /// <summary>Tạo ca. Gửi header <c>Idempotency-Key</c> để bấm trùng không tạo 2 lần.</summary>
        /// <response code="201">Envelope với ca vừa tạo.</response>
        /// <response code="409">DUPLICATE – trùng mã ca.</response>
        /// <response code="422">REF_NOT_FOUND (companyId) hoặc BUSINESS_RULE (giờ bắt đầu = giờ kết thúc).</response>
        [HttpPost]
        [Permission(Screen, PermissionAction.Add)]
        [Idempotent]
        [ProducesResponseType(typeof(ApiEnvelope<ShiftDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] ShiftUpsertRequest request, CancellationToken cancellationToken)
        {
            await ValidateAsync(request, currentId: null, cancellationToken);
            var entity = new Shift { Id = Guid.NewGuid() };
            Apply(entity, request);
            db.Shift.Add(entity);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, cancellationToken);
            // datetime SQL làm tròn ~3ms → đọc lại để ETag khớp với lần GET sau.
            await db.Entry(entity).ReloadAsync(cancellationToken);

            var dto = ToDto(entity);
            SetETag(dto.ETag);
            return CreatedAtAction(nameof(Get), new { id = entity.Id }, dto);
        }

        /// <summary>Sửa ca. Gửi <c>If-Match</c> = ETag đã đọc; lệch → 409 CONCURRENCY_CONFLICT.</summary>
        /// <response code="200">Envelope với ca sau sửa.</response>
        /// <response code="404">NOT_FOUND.</response>
        /// <response code="409">CONCURRENCY_CONFLICT hoặc DUPLICATE.</response>
        /// <response code="422">REF_NOT_FOUND / BUSINESS_RULE.</response>
        [HttpPut("{id:guid}")]
        [Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<ShiftDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(Guid id, [FromBody] ShiftUpsertRequest request, CancellationToken cancellationToken)
        {
            Shift entity = await FindAsync(id, tracking: true, cancellationToken);
            EnsureIfMatch(ComputeETag(entity.ModifiedDate, entity.CreatedDate));
            await ValidateAsync(request, currentId: id, cancellationToken);
            Apply(entity, request);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, cancellationToken);
            // datetime SQL làm tròn ~3ms → đọc lại để ETag khớp với lần GET sau.
            await db.Entry(entity).ReloadAsync(cancellationToken);

            var dto = ToDto(entity);
            SetETag(dto.ETag);
            return Ok(dto);
        }

        /// <summary>Xóa mềm ca. Đang được tham chiếu (ShiftAssignment, Order…) → 409 IN_USE.</summary>
        /// <response code="204">Đã xóa.</response>
        /// <response code="409">IN_USE – ca đang được tham chiếu.</response>
        /// <response code="404">NOT_FOUND.</response>
        [HttpDelete("{id:guid}")]
        [Permission(Screen, PermissionAction.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            Shift entity = await FindAsync(id, tracking: true, cancellationToken);
            EnsureIfMatch(ComputeETag(entity.ModifiedDate, entity.CreatedDate));
            var usages = await referenceGuard.FindUsagesAsync("Shift", id, cancellationToken);
            if (usages.Count > 0)
            {
                throw ApiException.Conflict(ErrorCodes.InUse,
                    "Ca đang được dùng ở: " + string.Join(", ", usages.Select(u => $"{u.Table} ({u.Count})")) + ". Không xóa được.");
            }
            db.Entry(entity).State = EntityState.Modified;
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Delete, cancellationToken);
            return NoContent();
        }

        private async Task<Shift> FindAsync(Guid id, bool tracking, CancellationToken cancellationToken)
        {
            IQueryable<Shift> q = tracking ? db.Shift : db.Shift.AsNoTracking();
            return await q.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
                ?? throw ApiException.NotFound("Không tìm thấy ca làm việc hoặc ca đã bị xóa.");
        }

        private async Task ValidateAsync(ShiftUpsertRequest request, Guid? currentId, CancellationToken cancellationToken)
        {
            // PROPOSED – BA/PO CONFIRM: ca có giờ bắt đầu = giờ kết thúc là vô nghĩa (ca 0 giờ hoặc 24 giờ).
            if (request.StartTime == request.EndTime)
            {
                throw ApiException.Unprocessable(ErrorCodes.BusinessRule, "Giờ bắt đầu và giờ kết thúc không được trùng nhau.", "endTime");
            }
            if (request.StartTime is { } start && (start < TimeSpan.Zero || start >= TimeSpan.FromDays(1)))
            {
                throw ApiException.Validation("startTime", "Giờ bắt đầu phải trong khoảng 00:00:00–23:59:59.");
            }
            if (request.EndTime is { } end && (end < TimeSpan.Zero || end >= TimeSpan.FromDays(1)))
            {
                throw ApiException.Validation("endTime", "Giờ kết thúc phải trong khoảng 00:00:00–23:59:59.");
            }
            if (request.CompanyId.HasValue
                && !await db.Company.AsNoTracking().AnyAsync(c => c.Id == request.CompanyId && !c.IsDeleted, cancellationToken))
            {
                throw ApiException.Unprocessable(ErrorCodes.RefNotFound, "Công ty không tồn tại hoặc đã bị xóa.", "companyId");
            }
            string code = request.Code!.Trim();
            if (await db.Shift.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.Code == code && x.Id != currentId, cancellationToken))
            {
                throw ApiException.Conflict(ErrorCodes.Duplicate, $"Mã ca '{code}' đã tồn tại.", "code");
            }
        }

        private static void Apply(Shift entity, ShiftUpsertRequest request)
        {
            entity.CompanyId = request.CompanyId;
            entity.Code = request.Code!.Trim();
            entity.Name = request.Name!.Trim();
            entity.StartTime = request.StartTime;
            entity.EndTime = request.EndTime;
            entity.Status = string.IsNullOrWhiteSpace(request.Status) ? StatusActive : request.Status;
            entity.Note = request.Note;
        }

        private static ShiftDto ToDto(Shift x) => new()
        {
            Id = x.Id,
            CompanyId = x.CompanyId,
            Code = x.Code,
            Name = x.Name,
            StartTime = x.StartTime,
            EndTime = x.EndTime,
            IsOvernight = x.StartTime.HasValue && x.EndTime.HasValue && x.EndTime < x.StartTime,
            Status = x.Status,
            Note = x.Note,
            ETag = ComputeETag(x.ModifiedDate, x.CreatedDate),
        };
    }
}
