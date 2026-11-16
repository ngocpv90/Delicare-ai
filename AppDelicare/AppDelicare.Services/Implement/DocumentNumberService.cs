using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T11 – mã chứng từ (BUSINESS_RULES §11). Cột đích lấy theo DEPRECATED_COLUMNS: đọc cả cột chuẩn lẫn cột cũ để không trùng với dữ liệu cũ.
    /// Tên bảng / cột lấy từ metadata EF (không ghép dữ liệu người dùng vào SQL); giá trị đều là tham số.
    /// </summary>
    public partial class DocumentNumberService(IUnitOfWork unitOfWork) : IDocumentNumberService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        private sealed record Target(Type Entity, string[] Properties);

        /// <param name="Prefix">Tiền tố (PH, XN…).</param>
        /// <param name="Digits">Số chữ số phần đếm; 0 = mã gốc không có số (KH, VS), lần sau thêm "-2", "-3"…</param>
        private sealed record Spec(string Prefix, int Digits, bool SuffixRequired, Target[] Targets);

        private static readonly Dictionary<DocumentKind, Spec> Specs = new()
        {
            [DocumentKind.Voucher] = new("PH", 4, false,
            [
                new(typeof(Order), ["OrderNumber", "OrderNo"]),
                new(typeof(GoodsReceipt), ["ReceiptNumber", "ReceiptNo"]),
                new(typeof(MaterialIssue), ["IssueNumber", "IssueNo"]),
                new(typeof(StockCount), ["CountNumber", "CountNo"]),
                // T16: bút toán điều chỉnh / chuyển kho tay (không có phiếu riêng – GAP-F-03 / F-04) lấy số PH- trên sổ cái.
                new(typeof(InventoryTransaction), ["TransactionNumber", "TransactionNo"]),
                new(typeof(PurchaseRequest), ["RequestNumber", "RequestNo"]),
                new(typeof(ProductionOrder), ["ProductionOrderNumber", "ProductionOrderNo"]),
                new(typeof(PackingOrder), ["PackingOrderNumber", "PackingNo"]),
                new(typeof(DeliveryOrder), ["DeliveryNumber", "DeliveryNo"]),
            ]),
            [DocumentKind.Approval] = new("XN", 3, false, [new(typeof(ApprovalRequest), ["RequestNo"])]),
            [DocumentKind.Complaint] = new("PA", 4, false, [new(typeof(CustomerComplaint), ["ComplaintNo"])]),
            [DocumentKind.PurchaseOrder] = new("PO", 4, false, [new(typeof(PurchaseOrder), ["PurchaseOrderNumber", "PONo"])]),
            [DocumentKind.Leave] = new("DN", 4, false, [new(typeof(LeaveRequest), ["RequestNo"])]),
            [DocumentKind.MaterialLot] = new("NL", 2, true, [new(typeof(MaterialLot), ["LotNumber", "LotNo"])]),
            [DocumentKind.ProductionBatch] = new("ME", 2, true, [new(typeof(ProductionBatch), ["BatchNumber", "BatchNo"])]),
            [DocumentKind.DeliveryRoute] = new("CX", 2, true, [new(typeof(DeliveryRoute), ["RouteNo"])]),
            [DocumentKind.ProductionPlan] = new("KH", 0, false, [new(typeof(ProductionPlan), ["PlanNumber", "PlanNo"])]),
            [DocumentKind.CleaningBatch] = new("VS", 0, true, [new(typeof(CleaningBatch), ["CleaningBatchNumber", "CleaningNo"])]),
        };

        public async Task<string> NextAsync(DocumentKind kind, DateTime date, string? suffix = null, CancellationToken cancellationToken = default)
        {
            var spec = Specs[kind];
            string? cleanSuffix = CleanSuffix(suffix);
            if (spec.SuffixRequired && cleanSuffix == null)
            {
                throw new ArgumentException($"Mã {spec.Prefix} cần hậu tố (mã NCC / ca / xe).", nameof(suffix));
            }
            // Mã gốc: PH-261116 / NL-261116-NCC01 / KH-261116 …
            string root = string.Join("-", new[] { spec.Prefix, date.ToString("yyMMdd", CultureInfo.InvariantCulture), cleanSuffix }.Where(x => !string.IsNullOrEmpty(x)));
            var db = _unitOfWork.DbContext;
            int? current;
            if (db.Database.IsRelational())
            {
                if (db.Database.CurrentTransaction == null)
                {
                    throw new InvalidOperationException("Sinh mã chứng từ phải nằm trong transaction đang mở (IUnitOfWork.OpenTransaction) – khóa giữ tới khi ghi xong.");
                }
                current = await MaxFromSqlAsync(db, spec, root, cancellationToken);
                // T50R1: cộng cả mã đã gán cho bản ghi đang theo dõi nhưng CHƯA lưu (vòng lặp sinh nhiều mã trước 1 lần CommitAsync –
                // phiếu nhập nhiều dòng → nhiều lô NL-, đề nghị mua → nhiều PO-). Trước đây SQL chỉ thấy dòng đã lưu → 2 lô cùng số NL-…-01
                // (InMemory đã tính bản ghi đang theo dõi nên test InMemory không bắt được).
                int? pending = MaxOf(db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity), spec, root);
                if (pending != null && (current == null || pending > current))
                {
                    current = pending;
                }
            }
            else
            {
                current = MaxFromMemory(db, spec, root);
            }
            if (spec.Digits == 0)
            {
                // KH-261116 → KH-261116-2 → KH-261116-3 … (null = chưa có mã gốc; 1 = đã có mã gốc).
                return current == null ? root : $"{root}-{current.Value + 1}";
            }
            int next = (current ?? 0) + 1;
            return $"{root}-{next.ToString(new string('0', spec.Digits), CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// SQL Server: khóa ứng dụng theo mã gốc (giữ tới hết transaction) rồi đọc số lớn nhất của mọi cột đích.
        /// Mã gốc trùng (Digits = 0) tính là 1. Không có mã nào → NULL.
        /// </summary>
        private static async Task<int?> MaxFromSqlAsync(DbContext db, Spec spec, string root, CancellationToken ct)
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @r INT;
                EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                IF @r < 0 THROW 50011, N'Không lấy được khóa sinh mã chứng từ (quá thời gian chờ).', 1;
                """, [new SqlParameter("@res", "DocNo:" + root)], ct);

            var parts = new List<string>();
            foreach (var target in spec.Targets)
            {
                var entityType = db.Model.FindEntityType(target.Entity) ?? throw new InvalidOperationException($"Không có entity {target.Entity.Name}.");
                string table = $"[{entityType.GetSchema() ?? "dbo"}].[{entityType.GetTableName()}]";
                foreach (string prop in target.Properties)
                {
                    var p = entityType.FindProperty(prop);
                    if (p == null)
                    {
                        continue; // cột cũ đã bỏ khỏi entity
                    }
                    string column = "[" + p.GetColumnName() + "]";
                    parts.Add($"SELECT {column} AS v FROM {table} WHERE {column} = @root OR {column} LIKE @pattern ESCAPE '\\'");
                }
            }
            string sql = $"""
                SELECT MAX(CASE WHEN v = @root THEN 1 ELSE TRY_CAST(SUBSTRING(v, LEN(@root) + 2, 20) AS INT) END) AS [Value]
                FROM ({string.Join(" UNION ALL ", parts)}) x
                """;
            string pattern = EscapeLike(root) + "-%";
            return await db.Database.SqlQueryRaw<int?>(sql, new SqlParameter("@root", root), new SqlParameter("@pattern", pattern))
                .SingleAsync(ct);
        }

        /// <summary>Provider không quan hệ (test InMemory): tính trên dữ liệu đã lưu + đang theo dõi.</summary>
        private static int? MaxFromMemory(DbContext db, Spec spec, string root)
        {
            var rows = spec.Targets.SelectMany(target =>
                ((IQueryable<object>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(target.Entity).Invoke(db, null)!).AsEnumerable())
                .Concat(db.ChangeTracker.Entries().Select(e => e.Entity));
            return MaxOf(rows, spec, root);
        }

        /// <summary>Số lớn nhất theo mã gốc trên một tập bản ghi (bỏ qua bản ghi khác loại entity đích). Mã gốc trùng (Digits = 0) tính là 1.</summary>
        private static int? MaxOf(IEnumerable<object> entities, Spec spec, string root)
        {
            int? max = null;
            var list = entities as IList<object> ?? entities.ToList();
            foreach (var target in spec.Targets)
            {
                foreach (var row in list.Where(r => r.GetType() == target.Entity))
                {
                    foreach (string prop in target.Properties)
                    {
                        if (target.Entity.GetProperty(prop)?.GetValue(row) is not string v)
                        {
                            continue;
                        }
                        int? n = v == root ? 1
                            : v.StartsWith(root + "-", StringComparison.Ordinal) && int.TryParse(v[(root.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int x) ? x
                            : null;
                        if (n != null && (max == null || n > max))
                        {
                            max = n;
                        }
                    }
                }
            }
            return max;
        }

        /// <summary>Hậu tố: không dấu, chữ hoa, chỉ A–Z 0–9 (ký tự khác → bỏ), tối đa 20 ký tự. Trống → null.</summary>
        private static string? CleanSuffix(string? suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }
            string s = NonAlnum().Replace(CommonHelper.RemoveSign4VietnameseString(suffix.Trim()).ToUpperInvariant(), string.Empty);
            if (s.Length > 20)
            {
                s = s[..20];
            }
            return s.Length == 0 ? null : s;
        }

        private static string EscapeLike(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c is '%' or '_' or '[' or '\\')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        [GeneratedRegex("[^A-Z0-9]")]
        private static partial Regex NonAlnum();
    }
}
