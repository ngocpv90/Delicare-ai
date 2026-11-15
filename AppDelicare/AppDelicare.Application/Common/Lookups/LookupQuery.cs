using System.Linq.Expressions;
using System.Reflection;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AppDelicare.Application.Common.Lookups
{
    /// <summary>Một dòng dropdown.</summary>
    /// <param name="Id">Id bản ghi.</param>
    /// <param name="Label">Nhãn hiển thị: DisplayName → Name → FullName → số chứng từ → Code.</param>
    /// <param name="Code">Mã (nếu bảng có cột Code).</param>
    /// <param name="Status">Trạng thái (nếu bảng có cột Status).</param>
    public sealed record LookupItem(Guid Id, string? Label, string? Code, string? Status);

    /// <summary>
    /// Spec 2.2 <c>GET /api/v1/meta/lookups/{table}</c> – dropdown tham chiếu cho mọi màn hình.
    /// <c>Ids</c> có giá trị → lấy đúng các Id đó (để hiện nhãn của giá trị đang chọn, kể cả không còn Active).
    /// </summary>
    public sealed record LookupQuery(string Table, string? Q = null, int Take = 50, IReadOnlyList<Guid>? Ids = null)
        : IQuery<IReadOnlyList<LookupItem>>;

    public sealed class LookupQueryValidator : AbstractValidator<LookupQuery>
    {
        public LookupQueryValidator()
        {
            RuleFor(x => x.Table).NotEmpty().WithMessage("Tên bảng là bắt buộc.")
                .Matches("^[A-Za-z_][A-Za-z0-9_]{0,127}$").WithMessage("Tên bảng chỉ gồm chữ, số, gạch dưới.");
            RuleFor(x => x.Take).InclusiveBetween(1, 200).WithMessage("take phải từ 1 đến 200.");
            RuleFor(x => x.Q).MaximumLength(100).WithMessage("q tối đa 100 ký tự.");
            RuleFor(x => x.Ids).Must(ids => ids == null || ids.Count <= 200).WithMessage("ids tối đa 200 phần tử.");
        }
    }

    /// <summary>
    /// Truy vấn động theo metadata EF (không SQL ghép chuỗi): bảng phải map trong DbContext và có cột Id kiểu Guid + ít nhất một cột nhãn.
    /// Bảng nhạy cảm không cho tra (PROPOSED – BA/PO CONFIRM, GAP-API-02).
    /// </summary>
    public sealed class LookupQueryHandler(DatabaseContext db) : IRequestHandler<LookupQuery, IReadOnlyList<LookupItem>>
    {
        public const string ActiveStatus = "Active";

        /// <summary>Không trả qua lookup: tài khoản / quyền / nhật ký / tin nhắn / tích hợp / dữ liệu y tế cũ.</summary>
        public static readonly IReadOnlySet<string> Blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "User", "UserClaim", "UserRole", "UserExternalIdentity", "Person", "AuditChange", "IntegrationLog", "LocationTracking",
            "InboundMessage", "OutboundMessage", "BotConfiguration", "CoreDataMergeConflict",
            "Patient", "PatientDetail", "MedicalExamination", "MedicineWarehouse",
        };

        private static readonly string[] LabelPriority = ["DisplayName", "Name", "FullName"];

        private static readonly MethodInfo RunMethod =
            typeof(LookupQueryHandler).GetMethod(nameof(RunAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

        public async Task<IReadOnlyList<LookupItem>> HandleAsync(LookupQuery request, CancellationToken cancellationToken)
        {
            string table = ReferenceMap.TableOfClass.TryGetValue(request.Table, out var real) ? real : request.Table;
            IEntityType? entity = db.Model.GetEntityTypes().FirstOrDefault(e => string.Equals(TableOf(e), table, StringComparison.OrdinalIgnoreCase));
            if (entity == null || Blocked.Contains(table) || Blocked.Contains(entity.ClrType.Name)
                || table.EndsWith("_Legacy_20261113", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(404, "NOT_FOUND", $"Không có danh mục tra cứu '{request.Table}'.", "table");
            }
            if ((Nullable.GetUnderlyingType(entity.FindProperty("Id")?.ClrType ?? typeof(object)) ?? entity.FindProperty("Id")?.ClrType) != typeof(Guid))
            {
                throw new BusinessRuleException(404, "NOT_FOUND", $"Bảng '{table}' không hỗ trợ tra cứu.", "table");
            }

            string? label = LabelPriority.FirstOrDefault(p => IsString(entity, p))
                ?? entity.GetProperties().Where(p => p.ClrType == typeof(string) && (p.Name.EndsWith("Number", StringComparison.Ordinal) || p.Name.EndsWith("No", StringComparison.Ordinal)))
                    .Select(p => p.Name).FirstOrDefault()
                ?? (IsString(entity, "Code") ? "Code" : null);
            if (label == null)
            {
                throw new BusinessRuleException(404, "NOT_FOUND", $"Bảng '{table}' không có cột nhãn để tra cứu.", "table");
            }

            var shape = new Shape(
                label,
                IsString(entity, "Code") ? "Code" : null,
                IsString(entity, "Status") ? "Status" : null,
                entity.FindProperty("IsDeleted") != null);

            var task = (Task<List<LookupItem>>)RunMethod.MakeGenericMethod(entity.ClrType)
                .Invoke(null, [db, shape, request, cancellationToken])!;
            return await task;
        }

        private sealed record Shape(string Label, string? Code, string? Status, bool HasIsDeleted);

        private static bool IsString(IEntityType entity, string property) => entity.FindProperty(property)?.ClrType == typeof(string);

        private static async Task<List<LookupItem>> RunAsync<T>(DatabaseContext db, Shape shape, LookupQuery request, CancellationToken cancellationToken)
            where T : class
        {
            IQueryable<T> query = db.Set<T>().AsNoTracking();
            if (shape.HasIsDeleted)
            {
                query = query.Where(e => !EF.Property<bool>(e, "IsDeleted"));
            }
            if (request.Ids is { Count: > 0 } ids)
            {
                var idList = ids.ToList();
                query = query.Where(e => idList.Contains(EF.Property<Guid>(e, "Id")));
            }
            else
            {
                if (shape.Status != null)
                {
                    // Chỉ bản ghi Active khi bảng có Status (Spec 2.2). Status NULL coi như Active (dữ liệu cũ chưa gán).
                    query = query.Where(e => EF.Property<string?>(e, shape.Status) == null || EF.Property<string?>(e, shape.Status) == ActiveStatus);
                }
                if (!string.IsNullOrWhiteSpace(request.Q))
                {
                    string q = request.Q.Trim();
                    query = shape.Code != null
                        ? query.Where(e => EF.Property<string?>(e, shape.Label)!.Contains(q) || EF.Property<string?>(e, shape.Code)!.Contains(q))
                        : query.Where(e => EF.Property<string?>(e, shape.Label)!.Contains(q));
                }
            }

            Expression<Func<T, LookupItem>> projection = e => new LookupItem(
                EF.Property<Guid>(e, "Id"),
                EF.Property<string?>(e, shape.Label),
                shape.Code == null ? null : EF.Property<string?>(e, shape.Code),
                shape.Status == null ? null : EF.Property<string?>(e, shape.Status));

            return await query
                .OrderBy(e => EF.Property<string?>(e, shape.Label))
                .Take(request.Ids is { Count: > 0 } ? request.Ids.Count : request.Take)
                .Select(projection)
                .ToListAsync(cancellationToken);
        }

        /// <summary>Tên bảng thật: [Table] trên class (đúng cả với provider không quan hệ như InMemory), không có thì GetTableName().</summary>
        private static string? TableOf(IEntityType e)
            => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>(e.ClrType)?.Name ?? e.GetTableName();
    }
}
