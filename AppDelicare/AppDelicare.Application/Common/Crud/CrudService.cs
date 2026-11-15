using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using AppDelicare.Application.Common.Lookups;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AppDelicare.Application.Common.Crud
{
    /// <summary>Tham số danh sách (Spec 2.1): page, pageSize, q, sort, lọc theo tên cột (camelCase), <c>xxxFrom</c> / <c>xxxTo</c> cho cột ngày.</summary>
    public sealed record CrudListQuery(int Page, int PageSize, string? Q, string? Sort, IReadOnlyDictionary<string, string> Filters);

    public sealed record CrudPage(IReadOnlyList<IDictionary<string, object?>> Items, int Total, int Page, int PageSize);

    public sealed record CrudItem(IDictionary<string, object?> Data, string ETag);

    /// <summary>Mô tả trường cho frontend dựng form (<c>GET /api/v1/meta/schema/{module}/{resource}</c>).</summary>
    public sealed record FieldSchema(string Field, string Column, string Type, bool Required, int? MaxLength, bool ReadOnly, string? RefTable, string? LabelField);

    public sealed record ResourceSchema(string Screen, string Route, string Table, bool CanCreate, bool CanUpdate, bool CanDelete,
        string? LabelField, IReadOnlyList<string> SearchFields, IReadOnlyList<string[]> UniqueFields, IReadOnlyList<FieldSchema> Fields);

    public interface ICrudService
    {
        Task<CrudPage> ListAsync(ResourceDefinition resource, CrudListQuery query, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<string, object?>> SummaryAsync(ResourceDefinition resource, CrudListQuery query, CancellationToken cancellationToken = default);
        Task<CrudItem> GetAsync(ResourceDefinition resource, Guid id, CancellationToken cancellationToken = default);
        Task<CrudItem> CreateAsync(ResourceDefinition resource, JsonElement body, CancellationToken cancellationToken = default);
        Task<CrudItem> UpdateAsync(ResourceDefinition resource, Guid id, JsonElement body, string? ifMatch, CancellationToken cancellationToken = default);
        Task DeleteAsync(ResourceDefinition resource, Guid id, string? ifMatch, CancellationToken cancellationToken = default);
        ResourceSchema Schema(ResourceDefinition resource);
    }

    /// <summary>
    /// T45B.1 – CRUD chuẩn (Spec 2.1) theo metadata DDL: không SQL ghép chuỗi (LINQ / expression tree có tham số), kiểm tra bắt buộc / độ dài / kiểu,
    /// UNIQUE (409 DUPLICATE), tham chiếu logic (422 REF_NOT_FOUND), If-Match (409 CONCURRENCY_CONFLICT), đang dùng (409 IN_USE),
    /// cột deprecated không trả / không nhận (ghi song song từ cột mới), không bao giờ trả mật khẩu.
    /// </summary>
    public sealed class CrudService(DatabaseContext db, IReferenceGuard referenceGuard) : ICrudService
    {
        public const string StatusProperty = "Status";

        public static string ComputeETag(DateTime? modified, DateTime created)
            => "\"" + (modified ?? created).Ticks.ToString(CultureInfo.InvariantCulture) + "\"";

        public static TableInfo InfoOf(ResourceDefinition r)
            => TableMetadata.All.TryGetValue(r.Table, out var t) ? t : throw new InvalidOperationException($"Thiếu metadata bảng {r.Table}.");

        private ICrudCore Core(ResourceDefinition r)
        {
            var info = InfoOf(r);
            var entity = db.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == info.Class)
                ?? throw new InvalidOperationException($"Không có entity {info.Class}.");
            return (ICrudCore)Activator.CreateInstance(typeof(CrudCore<>).MakeGenericType(entity.ClrType), db, entity, info, r, referenceGuard)!;
        }

        public Task<CrudPage> ListAsync(ResourceDefinition r, CrudListQuery q, CancellationToken ct = default) => Core(r).ListAsync(q, ct);
        public Task<IReadOnlyDictionary<string, object?>> SummaryAsync(ResourceDefinition r, CrudListQuery q, CancellationToken ct = default) => Core(r).SummaryAsync(q, ct);
        public Task<CrudItem> GetAsync(ResourceDefinition r, Guid id, CancellationToken ct = default) => Core(r).GetAsync(id, ct);
        public Task<CrudItem> CreateAsync(ResourceDefinition r, JsonElement body, CancellationToken ct = default)
            => r.CanCreate ? Core(r).CreateAsync(body, ct) : throw NotAllowed();
        public Task<CrudItem> UpdateAsync(ResourceDefinition r, Guid id, JsonElement body, string? ifMatch, CancellationToken ct = default)
            => r.CanUpdate ? Core(r).UpdateAsync(id, body, ifMatch, ct) : throw NotAllowed();
        public Task DeleteAsync(ResourceDefinition r, Guid id, string? ifMatch, CancellationToken ct = default)
            => r.CanDelete ? Core(r).DeleteAsync(id, ifMatch, ct) : throw NotAllowed();

        private static BusinessRuleException NotAllowed() => new(405, "METHOD_NOT_ALLOWED", "Màn hình này không cho thao tác này (chỉ đọc hoặc dùng API nghiệp vụ riêng).");

        public ResourceSchema Schema(ResourceDefinition r)
        {
            var info = InfoOf(r);
            var fields = info.Visible.Select(c => new FieldSchema(
                c.Field, c.Column, TypeName(c.SqlType), info.IsRequired(c), c.MaxLength, c.IsBase, c.RefTable,
                c.RefTable == null ? null : c.Field + "Label")).ToList();
            var label = info.LabelColumn == null ? null : info.Find(info.LabelColumn)?.Field;
            return new ResourceSchema(r.Screen, "/" + r.Route, r.Table, r.CanCreate, r.CanUpdate, r.CanDelete, label,
                info.SearchProperties.Select(Camel).ToList(),
                info.Uniques.Select(u => u.Select(col => info.Columns.First(c => c.Column == col).Field).ToArray()).ToList(),
                fields);
        }

        internal static string TypeName(string sqlType) => sqlType switch
        {
            "uniqueidentifier" => "guid",
            "nvarchar" or "varchar" or "nchar" or "char" or "sysname" => "string",
            "int" or "bigint" or "smallint" or "tinyint" => "integer",
            "decimal" or "numeric" or "money" or "float" or "real" => "number",
            "bit" => "boolean",
            "date" => "date",
            "datetime" or "datetime2" or "smalldatetime" => "datetime",
            "time" => "time",
            _ => sqlType,
        };

        internal static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];
    }

    internal interface ICrudCore
    {
        Task<CrudPage> ListAsync(CrudListQuery q, CancellationToken ct);
        Task<IReadOnlyDictionary<string, object?>> SummaryAsync(CrudListQuery q, CancellationToken ct);
        Task<CrudItem> GetAsync(Guid id, CancellationToken ct);
        Task<CrudItem> CreateAsync(JsonElement body, CancellationToken ct);
        Task<CrudItem> UpdateAsync(Guid id, JsonElement body, string? ifMatch, CancellationToken ct);
        Task DeleteAsync(Guid id, string? ifMatch, CancellationToken ct);
    }

    internal sealed class CrudCore<T>(DatabaseContext db, IEntityType entity, TableInfo info, ResourceDefinition resource, IReferenceGuard referenceGuard)
        : ICrudCore where T : class
    {
        private static readonly MethodInfo EfProperty = typeof(EF).GetMethod(nameof(EF.Property))!;

        private IProperty Prop(string name) => entity.FindProperty(name) ?? throw new InvalidOperationException($"{info.Class}.{name}");

        private static Expression Member(ParameterExpression e, IProperty p)
            => Expression.Call(EfProperty.MakeGenericMethod(p.ClrType), e, Expression.Constant(p.Name));

        private IQueryable<T> Base()
        {
            IQueryable<T> q = db.Set<T>().AsNoTracking();
            return entity.FindProperty("IsDeleted") != null ? q.Where(x => !EF.Property<bool>(x, "IsDeleted")) : q;
        }

        // ------------------------------------------------------------------ đọc
        private IQueryable<T> Filtered(CrudListQuery q)
        {
            var query = Base();
            var e = Expression.Parameter(typeof(T), "e");
            foreach (var (key, raw) in q.Filters)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }
                if (resource.CustomFilters.TryGetValue(key, out var custom))
                {
                    query = (IQueryable<T>)custom(query, raw);
                    continue;
                }
                string field = key;
                string op = "eq";
                if (key.EndsWith("From", StringComparison.Ordinal) && info.FindByField(key[..^4]) is { IsDate: true })
                {
                    field = key[..^4];
                    op = "ge";
                }
                else if (key.EndsWith("To", StringComparison.Ordinal) && info.FindByField(key[..^2]) is { IsDate: true })
                {
                    field = key[..^2];
                    op = "le";
                }
                var column = info.FindByField(field);
                if (column == null || column.IsDeprecated || ColumnInfo.Hidden.Contains(column.Property))
                {
                    throw Validation(key, $"Không lọc được theo '{key}'.");
                }
                var p = Prop(column.Property);
                object? value = Parse(raw, p.ClrType, key);
                Expression body = op switch
                {
                    "ge" => Expression.GreaterThanOrEqual(Member(e, p), Expression.Constant(value, p.ClrType)),
                    "le" => Expression.LessThanOrEqual(Member(e, p), Expression.Constant(value, p.ClrType)),
                    _ => Expression.Equal(Member(e, p), Expression.Constant(value, p.ClrType)),
                };
                query = query.Where(Expression.Lambda<Func<T, bool>>(body, e));
            }
            if (!string.IsNullOrWhiteSpace(q.Q) && info.SearchProperties.Count > 0)
            {
                string term = q.Q.Trim();
                Expression? any = null;
                var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
                foreach (string name in info.SearchProperties)
                {
                    var m = Member(e, Prop(name));
                    Expression test = Expression.AndAlso(Expression.NotEqual(m, Expression.Constant(null, typeof(string))),
                        Expression.Call(m, contains, Expression.Constant(term)));
                    any = any == null ? test : Expression.OrElse(any, test);
                }
                query = query.Where(Expression.Lambda<Func<T, bool>>(any!, e));
            }
            return query;
        }

        private IQueryable<T> Sorted(IQueryable<T> query, string? sort)
        {
            var e = Expression.Parameter(typeof(T), "e");
            var parts = string.IsNullOrWhiteSpace(sort)
                ? [(entity.FindProperty("CreatedDate") != null ? "createdDate" : "id") + ":desc"]
                : sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool first = true;
            foreach (string part in parts)
            {
                string[] bits = part.Split(':', StringSplitOptions.TrimEntries);
                bool desc = bits.Length > 1 && bits[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
                if (bits.Length > 2 || (bits.Length == 2 && !desc && !bits[1].Equals("asc", StringComparison.OrdinalIgnoreCase)))
                {
                    throw Validation("sort", $"Sort '{part}' sai định dạng (field:asc|desc).");
                }
                var column = info.FindByField(bits[0]);
                if (column == null || column.IsDeprecated || ColumnInfo.Hidden.Contains(column.Property))
                {
                    throw Validation("sort", $"Không sắp xếp được theo '{bits[0]}'.");
                }
                var p = Prop(column.Property);
                var lambda = Expression.Lambda(Member(e, p), e);
                string method = (first ? "OrderBy" : "ThenBy") + (desc ? "Descending" : string.Empty);
                query = (IQueryable<T>)typeof(Queryable).GetMethods()
                    .First(m => m.Name == method && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), p.ClrType)
                    .Invoke(null, [query, lambda])!;
                first = false;
            }
            return query;
        }

        public async Task<CrudPage> ListAsync(CrudListQuery q, CancellationToken ct)
        {
            var filtered = Filtered(q);
            int total = await filtered.CountAsync(ct);
            var rows = await Sorted(filtered, q.Sort).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
            var items = rows.Select(ToDto).ToList();
            await AddLabelsAsync(items, ct);
            if (resource.Enrich != null && items.Count > 0)
            {
                await resource.Enrich(new CrudEnrichContext(items, db, ct));
            }
            return new CrudPage(items, total, q.Page, q.PageSize);
        }

        public async Task<IReadOnlyDictionary<string, object?>> SummaryAsync(CrudListQuery q, CancellationToken ct)
        {
            var filtered = Filtered(q);
            int total = await filtered.CountAsync(ct);
            var result = new Dictionary<string, object?> { ["total"] = total };
            if (entity.FindProperty(CrudService.StatusProperty)?.ClrType == typeof(string))
            {
                result["byStatus"] = await filtered.GroupBy(x => EF.Property<string?>(x, CrudService.StatusProperty))
                    .Select(g => new { status = g.Key, count = g.Count() }).OrderBy(x => x.status).ToListAsync(ct);
            }
            return result;
        }

        public async Task<CrudItem> GetAsync(Guid id, CancellationToken ct)
        {
            var row = await Base().FirstOrDefaultAsync(x => EF.Property<Guid>(x, "Id") == id, ct) ?? throw NotFound();
            return await ItemAsync(row, ct);
        }

        private async Task<CrudItem> ItemAsync(T row, CancellationToken ct)
        {
            var dto = ToDto(row);
            await AddLabelsAsync([dto], ct);
            if (resource.Enrich != null)
            {
                await resource.Enrich(new CrudEnrichContext([dto], db, ct));
            }
            string etag = row is BaseEntity b ? CrudService.ComputeETag(b.ModifiedDate, b.CreatedDate) : "\"0\"";
            return new CrudItem(dto, etag);
        }

        private IDictionary<string, object?> ToDto(T row)
        {
            var dto = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var c in info.Visible)
            {
                if (entity.FindProperty(c.Property) is { PropertyInfo: { } pi })
                {
                    dto[c.Field] = pi.GetValue(row);
                }
            }
            return dto;
        }

        /// <summary>Thêm <c>{field}Label</c> cho cột tham chiếu (một truy vấn cho mỗi bảng đích).</summary>
        private async Task AddLabelsAsync(IReadOnlyList<IDictionary<string, object?>> items, CancellationToken ct)
        {
            foreach (var c in info.Visible.Where(c => c.RefTable != null && c.SqlType == "uniqueidentifier"))
            {
                var ids = items.Select(i => i.TryGetValue(c.Field, out var v) ? v as Guid? : null).Where(g => g is not null && g != Guid.Empty)
                    .Select(g => g!.Value).Distinct().ToList();
                foreach (var i in items)
                {
                    i[c.Field + "Label"] = null;
                }
                if (ids.Count == 0 || LookupQueryHandler.Blocked.Contains(c.RefTable!))
                {
                    continue;
                }
                IReadOnlyList<LookupItem> labels;
                try
                {
                    labels = await new LookupQueryHandler(db).HandleAsync(new LookupQuery(c.RefTable!, Take: ids.Count, Ids: ids), ct);
                }
                catch (BusinessRuleException)
                {
                    continue; // bảng đích không có cột nhãn
                }
                var map = labels.ToDictionary(l => l.Id, l => l.Label);
                foreach (var i in items)
                {
                    if (i[c.Field] is Guid g && map.TryGetValue(g, out var label))
                    {
                        i[c.Field + "Label"] = label;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ ghi
        public async Task<CrudItem> CreateAsync(JsonElement body, CancellationToken ct)
        {
            var row = (T)Activator.CreateInstance(typeof(T))!;
            if (row is BaseEntity be)
            {
                be.Id = Guid.NewGuid();
            }
            var provided = Read(body, isCreate: true);
            Apply(row, provided);
            await ValidateAsync(row, provided, id: null, ct);
            db.Set<T>().Add(row);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            await AfterSaveAsync(row, ((BaseEntity)(object)row).Id, ct);
            await db.Entry(row).ReloadAsync(ct);
            return await ItemAsync(row, ct);
        }

        public async Task<CrudItem> UpdateAsync(Guid id, JsonElement body, string? ifMatch, CancellationToken ct)
        {
            var row = await db.Set<T>().FirstOrDefaultAsync(x => EF.Property<Guid>(x, "Id") == id
                && (entity.FindProperty("IsDeleted") == null || !EF.Property<bool>(x, "IsDeleted")), ct) ?? throw NotFound();
            EnsureIfMatch(row, ifMatch);
            var provided = Read(body, isCreate: false);
            Apply(row, provided);
            await ValidateAsync(row, provided, id, ct);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            await AfterSaveAsync(row, id, ct);
            await db.Entry(row).ReloadAsync(ct);
            return await ItemAsync(row, ct);
        }

        public async Task DeleteAsync(Guid id, string? ifMatch, CancellationToken ct)
        {
            var row = await db.Set<T>().FirstOrDefaultAsync(x => EF.Property<Guid>(x, "Id") == id
                && (entity.FindProperty("IsDeleted") == null || !EF.Property<bool>(x, "IsDeleted")), ct) ?? throw NotFound();
            EnsureIfMatch(row, ifMatch);
            var usages = await referenceGuard.FindUsagesAsync(info.Table, id, ct);
            if (usages.Count > 0)
            {
                throw new BusinessRuleException(409, "IN_USE",
                    "Đang được dùng ở: " + string.Join(", ", usages.Select(u => $"{u.Table} ({u.Count})")) + ". Không xóa được.");
            }
            db.Entry(row).State = EntityState.Modified;
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Delete, ct);
        }

        private static void EnsureIfMatch(T row, string? ifMatch)
        {
            if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*" || row is not BaseEntity b)
            {
                return;
            }
            string current = CrudService.ComputeETag(b.ModifiedDate, b.CreatedDate);
            if (!ifMatch.Split(',').Select(v => v.Trim().Replace("W/", string.Empty, StringComparison.Ordinal)).Contains(current))
            {
                throw new BusinessRuleException(409, "CONCURRENCY_CONFLICT", "Bản ghi đã được người khác sửa. Tải lại dữ liệu rồi thử lại.");
            }
        }

        /// <summary>Đọc JSON: chỉ nhận cột ghi được; trường lạ / deprecated / chỉ đọc → 400.</summary>
        private Dictionary<string, object?> Read(JsonElement body, bool isCreate)
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                throw Validation("body", "Body phải là một đối tượng JSON.");
            }
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            var errors = new List<(string, string)>();
            foreach (var prop in body.EnumerateObject())
            {
                var column = info.FindByField(prop.Name);
                if (column == null || column.IsDeprecated || ColumnInfo.Hidden.Contains(column.Property))
                {
                    if (prop.Name is "id" or "etag" or "createdDate" or "createdBy" or "modifiedDate" or "modifiedBy" || prop.Name.EndsWith("Label", StringComparison.Ordinal))
                    {
                        continue; // client gửi lại nguyên DTO – bỏ qua trường chỉ đọc
                    }
                    errors.Add((prop.Name, column?.IsDeprecated == true ? $"'{prop.Name}' là cột cũ (deprecated) – dùng cột mới." : $"Trường '{prop.Name}' không hợp lệ."));
                    continue;
                }
                if (column.IsBase)
                {
                    continue;
                }
                var p = Prop(column.Property);
                try
                {
                    object? v = Convert(prop.Value, p.ClrType, column);
                    if (v is string s && column.MaxLength is > 0 && s.Length > column.MaxLength)
                    {
                        errors.Add((column.Field, $"Tối đa {column.MaxLength} ký tự."));
                        continue;
                    }
                    values[column.Property] = v;
                }
                catch (FormatException ex)
                {
                    errors.Add((column.Field, ex.Message));
                }
            }
            if (isCreate)
            {
                foreach (var c in info.Writable.Where(c => info.IsRequired(c) && !values.ContainsKey(c.Property)))
                {
                    errors.Add((c.Field, "Bắt buộc."));
                }
            }
            foreach (var (prop, v) in values)
            {
                var c = info.Find(prop)!;
                if (info.IsRequired(c) && (v == null || (v is string s && string.IsNullOrWhiteSpace(s))))
                {
                    errors.Add((c.Field, "Bắt buộc."));
                }
            }
            if (errors.Count > 0)
            {
                throw new ValidationFailedException(errors.Distinct().ToList());
            }
            return values;
        }

        private void Apply(T row, Dictionary<string, object?> values)
        {
            var entry = db.Entry(row);
            foreach (var (prop, v) in values)
            {
                entry.Property(prop).CurrentValue = v;
            }
            // DEPRECATED_COLUMNS: ghi song song cột cũ từ cột mới.
            foreach (var old in info.Columns.Where(c => c.IsDeprecated && c.ReplacedBy != null && !c.IsBase))
            {
                if (entity.FindProperty(old.Property) is { } op && entity.FindProperty(old.ReplacedBy!) is { } np
                    && (Nullable.GetUnderlyingType(op.ClrType) ?? op.ClrType) == (Nullable.GetUnderlyingType(np.ClrType) ?? np.ClrType))
                {
                    object? nv = entry.Property(np.Name).CurrentValue;
                    if (nv != null || op.IsNullable)
                    {
                        entry.Property(op.Name).CurrentValue = nv;
                    }
                }
            }
        }

        private async Task ValidateAsync(T row, Dictionary<string, object?> provided, Guid? id, CancellationToken ct)
        {
            var entry = db.Entry(row);
            var current = info.Columns.Where(c => entity.FindProperty(c.Property) != null)
                .ToDictionary(c => c.Property, c => entry.Property(c.Property).CurrentValue);

            // Tham chiếu logic (thay FK) → 422 REF_NOT_FOUND.
            foreach (var c in info.Writable.Where(c => c.RefTable != null && provided.ContainsKey(c.Property)))
            {
                if (provided[c.Property] is Guid g && g != Guid.Empty && !await ExistsAsync(c.RefTable!, g, ct))
                {
                    throw new BusinessRuleException(422, "REF_NOT_FOUND", $"{c.Field}: không tồn tại trong {c.RefTable} hoặc đã bị xóa.", c.Field);
                }
            }

            // UNIQUE của DDL + Code (nếu bảng có Code và chưa có UNIQUE riêng) → 409 DUPLICATE.
            var groups = info.Uniques.Select(u => u.Select(col => info.Columns.First(c => c.Column == col).Property).ToArray()).ToList();
            if (info.Find("Code") is { IsDeprecated: false, IsString: true } && !groups.Any(g => g.Length == 1 && g[0] == "Code"))
            {
                groups.Add(["Code"]);
            }
            foreach (var group in groups.Where(g => g.Any(provided.ContainsKey)))
            {
                if (group.Any(p => current[p] == null))
                {
                    continue;
                }
                var e = Expression.Parameter(typeof(T), "e");
                Expression body = Expression.NotEqual(Member(e, Prop("Id")), Expression.Constant(id ?? Guid.Empty, Prop("Id").ClrType));
                foreach (string p in group)
                {
                    var ip = Prop(p);
                    body = Expression.AndAlso(body, Expression.Equal(Member(e, ip), Expression.Constant(current[p], ip.ClrType)));
                }
                if (await Base().AnyAsync(Expression.Lambda<Func<T, bool>>(body, e), ct))
                {
                    string fields = string.Join(", ", group.Select(CrudService.Camel));
                    throw new BusinessRuleException(409, "DUPLICATE", $"Đã tồn tại bản ghi trùng ({fields}).", CrudService.Camel(group[0]));
                }
            }

            if (resource.Rules != null)
            {
                await resource.Rules(new CrudRuleContext(current, id, db, ct));
            }
        }

        private async Task AfterSaveAsync(T row, Guid id, CancellationToken ct)
        {
            if (resource.AfterSave == null)
            {
                return;
            }
            var entry = db.Entry(row);
            var current = info.Columns.Where(c => entity.FindProperty(c.Property) != null).ToDictionary(c => c.Property, c => entry.Property(c.Property).CurrentValue);
            await resource.AfterSave(new CrudRuleContext(current, id, db, ct));
        }

        private async Task<bool> ExistsAsync(string table, Guid id, CancellationToken ct)
        {
            var target = db.Model.GetEntityTypes().FirstOrDefault(t =>
                string.Equals(t.ClrType.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>()?.Name ?? t.GetTableName(), table, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                return true; // bảng đích chưa map – không kiểm được (80 tham chiếu chưa rõ: chỉ kiểm định dạng)
            }
            var method = typeof(CrudCore<T>).GetMethod(nameof(ExistsCore), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(target.ClrType);
            return await (Task<bool>)method.Invoke(null, [db, id, target.FindProperty("IsDeleted") != null, ct])!;
        }

        private static Task<bool> ExistsCore<TTarget>(DatabaseContext db, Guid id, bool hasIsDeleted, CancellationToken ct) where TTarget : class
            => hasIsDeleted
                ? db.Set<TTarget>().AnyAsync(x => EF.Property<Guid>(x, "Id") == id && !EF.Property<bool>(x, "IsDeleted"), ct)
                : db.Set<TTarget>().AnyAsync(x => EF.Property<Guid>(x, "Id") == id, ct);

        // ------------------------------------------------------------------ chuyển kiểu
        private static object? Convert(JsonElement v, Type clr, ColumnInfo c)
        {
            if (v.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            Type t = Nullable.GetUnderlyingType(clr) ?? clr;
            try
            {
                if (t == typeof(string))
                {
                    return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new FormatException("Phải là chuỗi.");
                }
                if (t == typeof(Guid))
                {
                    return v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var g) ? g : throw new FormatException("Phải là GUID.");
                }
                if (t == typeof(bool))
                {
                    return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new FormatException("Phải là true / false.");
                }
                if (t == typeof(int))
                {
                    return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : throw new FormatException("Phải là số nguyên.");
                }
                if (t == typeof(long))
                {
                    return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long l) ? l : throw new FormatException("Phải là số nguyên.");
                }
                if (t == typeof(decimal))
                {
                    return v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out decimal d) ? d : throw new FormatException("Phải là số.");
                }
                if (t == typeof(double))
                {
                    return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : throw new FormatException("Phải là số.");
                }
                if (t == typeof(DateTime))
                {
                    if (v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        return c.SqlType == "date" ? dt.Date : dt;
                    }
                    throw new FormatException(c.SqlType == "date" ? "Phải là ngày YYYY-MM-DD." : "Phải là ngày giờ ISO 8601.");
                }
                if (t == typeof(TimeSpan))
                {
                    return v.ValueKind == JsonValueKind.String && TimeSpan.TryParseExact(v.GetString(), ["hh\\:mm\\:ss", "hh\\:mm"], CultureInfo.InvariantCulture, out var ts)
                        && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1) ? ts : throw new FormatException("Phải là giờ HH:mm:ss.");
                }
            }
            catch (InvalidOperationException)
            {
                throw new FormatException("Sai kiểu dữ liệu.");
            }
            throw new FormatException($"Kiểu {t.Name} chưa hỗ trợ.");
        }

        private static object? Parse(string raw, Type clr, string field)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(raw));
            Type t = Nullable.GetUnderlyingType(clr) ?? clr;
            try
            {
                if (t == typeof(string)) return raw;
                if (t == typeof(Guid)) return Guid.Parse(raw);
                if (t == typeof(bool)) return bool.Parse(raw);
                if (t == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
                if (t == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
                if (t == typeof(decimal)) return decimal.Parse(raw, CultureInfo.InvariantCulture);
                if (t == typeof(DateTime)) return DateTime.Parse(raw, CultureInfo.InvariantCulture);
                if (t == typeof(TimeSpan)) return TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw Validation(field, $"Giá trị lọc '{raw}' sai kiểu.");
            }
            throw Validation(field, "Không lọc được theo cột này.");
        }

        private static ValidationFailedException Validation(string field, string message) => new([(field, message)]);

        private static BusinessRuleException NotFound() => new(404, "NOT_FOUND", "Không tìm thấy hoặc đã bị xóa.");
    }
}
