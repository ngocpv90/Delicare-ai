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

        /// <param name="Prefix">Tiền tố (DH, XN, KHB…).</param>
        /// <param name="Digits">Số chữ số phần đếm; 0 = mã gốc không có số (KS, VS), lần sau thêm "-2", "-3"…</param>
        /// <param name="Catalog">true = danh mục (dạng A, không phần ngày, không hậu tố).</param>
        private sealed record Spec(string Prefix, int Digits, bool SuffixRequired, Target[] Targets, bool Catalog = false);

        /// <summary>
        /// D-63 / BUSINESS_RULES §11 – nơi DUY NHẤT đăng ký tiền tố. Không tiền tố nào là phần đầu của tiền tố khác (test <c>CodeRulesTests</c> chặn).
        /// Chứng từ phát sinh trước D-63 giữ số cũ (PH-, KH-, NL-…) – số cũ khác tiền tố nên không tính vào dãy mới.
        /// </summary>
        private static readonly Dictionary<DocumentKind, Spec> Specs = new()
        {
            // ---------- Chứng từ (dạng B) ----------
            [DocumentKind.Order] = new("DH", 4, false, [new(typeof(Order), ["OrderNumber", "OrderNo"])]),
            // BE2.2 (spec 6.1): HD-YYMMDD-NNN; ghi song song ContractNumber / ContractNo (cột cũ).
            [DocumentKind.Contract] = new("HD", 3, false, [new(typeof(Contract), ["ContractNumber", "ContractNo"])]),
            [DocumentKind.Menu] = new("NU", 3, false, [new(typeof(Menu), ["Code"])]),
            [DocumentKind.MenuChange] = new("DM", 4, false, [new(typeof(MenuChangeRequest), ["RequestNo"])]),
            [DocumentKind.PurchaseRequest] = new("MH", 4, false, [new(typeof(PurchaseRequest), ["RequestNumber", "RequestNo"])]),
            [DocumentKind.PurchaseOrder] = new("PO", 4, false, [new(typeof(PurchaseOrder), ["PurchaseOrderNumber", "PONo"])]),
            [DocumentKind.GoodsReceipt] = new("PN", 4, false, [new(typeof(GoodsReceipt), ["ReceiptNumber", "ReceiptNo"])]),
            [DocumentKind.MaterialIssue] = new("PX", 4, false, [new(typeof(MaterialIssue), ["IssueNumber", "IssueNo"])]),
            [DocumentKind.StockCount] = new("KK", 4, false, [new(typeof(StockCount), ["CountNumber", "CountNo"])]),
            // T16: bút toán điều chỉnh / chuyển kho tay (không có phiếu riêng – GAP-F-03 / F-04) lấy số GD- trên sổ cái.
            [DocumentKind.InventoryTransaction] = new("GD", 4, false, [new(typeof(InventoryTransaction), ["TransactionNumber", "TransactionNo"])]),
            [DocumentKind.MaterialLot] = new("LO", 2, true, [new(typeof(MaterialLot), ["LotNumber", "LotNo"])]),
            [DocumentKind.ProductionPlan] = new("KS", 0, false, [new(typeof(ProductionPlan), ["PlanNumber", "PlanNo"])]),
            [DocumentKind.ProductionOrder] = new("LS", 4, false, [new(typeof(ProductionOrder), ["ProductionOrderNumber", "ProductionOrderNo"])]),
            [DocumentKind.ProductionBatch] = new("ME", 2, true, [new(typeof(ProductionBatch), ["BatchNumber", "BatchNo"])]),
            [DocumentKind.PackingOrder] = new("DG", 4, false, [new(typeof(PackingOrder), ["PackingOrderNumber", "PackingNo"])]),
            [DocumentKind.DeliveryOrder] = new("GH", 4, false, [new(typeof(DeliveryOrder), ["DeliveryNumber", "DeliveryNo"])]),
            [DocumentKind.DeliveryRoute] = new("CX", 2, true, [new(typeof(DeliveryRoute), ["RouteNo"])]),
            [DocumentKind.CleaningBatch] = new("VS", 0, true, [new(typeof(CleaningBatch), ["CleaningBatchNumber", "CleaningNo"])]),
            [DocumentKind.Complaint] = new("PA", 4, false, [new(typeof(CustomerComplaint), ["ComplaintNo"])]),
            [DocumentKind.Approval] = new("XN", 3, false, [new(typeof(ApprovalRequest), ["RequestNo"])]),
            [DocumentKind.Leave] = new("DN", 4, false, [new(typeof(LeaveRequest), ["RequestNo"])]),
            [DocumentKind.ShiftSwap] = new("DC", 4, false, [new(typeof(ShiftSwapRequest), ["RequestNo"])]),
            [DocumentKind.QcInspection] = new("QC", 4, false, [new(typeof(QcInspection), ["InspectionNumber"])]),
            [DocumentKind.MaterialRequirement] = new("MR", 3, false, [new(typeof(MaterialRequirement), ["RequirementNo"])]),

            // ---------- Danh mục (dạng A) ----------
            // KHB / KHC dùng chung cột Customer.Code nhưng đếm riêng theo tiền tố (mã gốc khác nhau).
            [DocumentKind.CustomerBusiness] = new("KHB", 5, false, [new(typeof(Customer), ["Code"])], Catalog: true),
            [DocumentKind.CustomerIndividual] = new("KHC", 5, false, [new(typeof(Customer), ["Code"])], Catalog: true),
            [DocumentKind.Supplier] = new("NC", 5, false, [new(typeof(Supplier), ["Code"])], Catalog: true),
            [DocumentKind.Ingredient] = new("NL", 5, false, [new(typeof(Ingredient), ["Code"])], Catalog: true),
            [DocumentKind.Dish] = new("MA", 5, false, [new(typeof(Dish), ["Code"])], Catalog: true),
            [DocumentKind.Warehouse] = new("KO", 5, false, [new(typeof(Warehouse), ["Code"])], Catalog: true),
            [DocumentKind.Vehicle] = new("XE", 5, false, [new(typeof(Vehicle), ["Code"])], Catalog: true),
            [DocumentKind.Employee] = new("NV", 5, false, [new(typeof(Employee), ["EmployeeCode", "Code"])], Catalog: true),
            [DocumentKind.DeliveryLocation] = new("DP", 5, false, [new(typeof(CustomerDeliveryLocation), ["Code"])], Catalog: true),
            [DocumentKind.PricePolicy] = new("BG", 5, false, [new(typeof(PricePolicy), ["Code"])], Catalog: true),
            [DocumentKind.DishCategory] = new("NM", 5, false, [new(typeof(DishCategory), ["Code"])], Catalog: true),
            [DocumentKind.IngredientCategory] = new("NN", 5, false, [new(typeof(IngredientCategory), ["Code"])], Catalog: true),
        };

        /// <summary>Thông tin tiền tố cho API quy tắc mã / test (không lộ cấu trúc nội bộ).</summary>
        public sealed record KindInfo(DocumentKind Kind, string Prefix, bool Catalog, int Digits, bool SuffixRequired, IReadOnlyList<(Type Entity, string[] Properties)> Targets)
        {
            /// <summary>Mẫu hiển thị: <c>KHB-NNNNN</c>, <c>DH-YYMMDD-NNNN</c>, <c>ME-YYMMDD-&lt;HẬU TỐ&gt;-NN</c>, <c>KS-YYMMDD</c>.</summary>
            public string Pattern => Catalog ? $"{Prefix}-{new string('N', Digits)}"
                : string.Join("-", new[] { Prefix, "YYMMDD", SuffixRequired ? "<HẬU TỐ>" : null, Digits > 0 ? new string('N', Digits) : null }.Where(x => x != null));
        }

        public static IReadOnlyList<KindInfo> Kinds { get; } = Specs
            .Select(x => new KindInfo(x.Key, x.Value.Prefix, x.Value.Catalog, x.Value.Digits, x.Value.SuffixRequired,
                x.Value.Targets.Select(t => (t.Entity, t.Properties)).ToList()))
            .ToList();

        public static KindInfo Info(DocumentKind kind) => Kinds.Single(k => k.Kind == kind);

        public Task<string> NextCodeAsync(DocumentKind kind, CancellationToken cancellationToken = default)
        {
            if (!Specs[kind].Catalog)
            {
                throw new ArgumentException($"{kind} là chứng từ – dùng NextAsync(kind, date).", nameof(kind));
            }
            return NextAsync(kind, DateTime.MinValue, null, cancellationToken);
        }

        public async Task<string> NextAsync(DocumentKind kind, DateTime date, string? suffix = null, CancellationToken cancellationToken = default)
        {
            var spec = Specs[kind];
            string? cleanSuffix = spec.Catalog ? null : CleanSuffix(suffix);
            if (spec.SuffixRequired && cleanSuffix == null)
            {
                throw new ArgumentException($"Mã {spec.Prefix} cần hậu tố (mã NCC / ca / xe).", nameof(suffix));
            }
            // Mã gốc: DH-261116 / LO-261116-NCC01 / KS-261116 … ; danh mục: KHB / NC …
            string root = spec.Catalog ? spec.Prefix
                : string.Join("-", new[] { spec.Prefix, date.ToString("yyMMdd", CultureInfo.InvariantCulture), cleanSuffix }.Where(x => !string.IsNullOrEmpty(x)));
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
