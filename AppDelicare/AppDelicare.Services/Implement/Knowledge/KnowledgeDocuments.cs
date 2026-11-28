using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using static AppDelicare.Services.Implement.Knowledge.KnowledgeVault;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed class KnowledgeDocumentModel
    {
        public string Path { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? SoHieu { get; set; }
        public DateTime? Date { get; set; }
        /// <summary>Ngày hết hạn hợp đồng (frontmatter het_han) – nhắc trước Knowledge:ContractExpiryNoticeDays.</summary>
        public DateTime? ExpiresOn { get; set; }
        public string? FileName { get; set; }
        public bool CanDownload { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>AI-22.5: tài liệu kho gắn 1 đối tượng (khối "Kho tài liệu" trong tab Tài liệu của trang chi tiết).</summary>
    public sealed class KnowledgeEntityDocsModel
    {
        /// <summary>Mã đối tượng ghi vào doi_tuong khi tải lên tại trang chi tiết.</summary>
        public string? Code { get; set; }
        /// <summary>Vùng tải lên mặc định (20-HD / 40-HD / 70-HS) + loại gợi ý; CanUpload = có quyền Sửa vùng đó.</summary>
        public string UploadFolder { get; set; } = string.Empty;
        public List<string> UploadTypes { get; set; } = [];
        public bool CanUpload { get; set; }
        public List<KnowledgeDocumentModel> ListDocument { get; set; } = [];
    }

    /// <summary>
    /// AI-22.5 (D-141 i): hồ sơ gắn đối tượng – tab "Tài liệu" trên trang chi tiết Khách hàng / Hợp đồng / NCC / Nhân viên hiện tài liệu gốc trong kho có
    /// <c>doi_tuong</c> (hoặc refs) là mã của bản ghi – CHỈ tài liệu người xem có quyền vùng; tải lên ngay tại trang (vùng mặc định theo loại đối tượng).
    /// Nhắc hết hạn hợp đồng: khách (Contract.EffectiveTo, hợp đồng Hiệu lực) + NCC / lao động (frontmatter het_han) trước <c>Knowledge:ContractExpiryNoticeDays</c> (30), 7 và 1 ngày.
    /// </summary>
    public partial class KnowledgeService
    {
        public static readonly string[] DocEntityTypes = ["customer", "contract", "supplier", "employee"];

        private static (string Folder, string[] Types) UploadTarget(string type) => type switch
        {
            "supplier" => ("40-HD", ["hop-dong-ncc", "tai-lieu"]),
            "employee" => (Personal, ["ho-so-nv", "hop-dong-ld"]),
            _ => ("20-HD", ["hop-dong-kh", "tai-lieu"]),
        };

        private async Task<List<string>> DocCodesAsync(string type, Guid id)
        {
            var db = unitOfWork.DbContext;
            IEnumerable<string?> codes = type switch
            {
                "contract" => await db.Contract.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.ContractNumber, x.ContractNo }).FirstOrDefaultAsync() ?? [],
                "employee" => await db.Employee.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.EmployeeCode, x.Code }).FirstOrDefaultAsync() ?? [],
                _ => await EntityCodesAsync(type, id),
            };
            return [.. codes.Where(c => !string.IsNullOrWhiteSpace(c) && c.Trim().Length > 2).Select(c => c!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        public async Task<IResult<KnowledgeEntityDocsModel>> GetDocuments(string? type, Guid id)
        {
            string t = (type ?? string.Empty).Trim().ToLowerInvariant();
            if (!DocEntityTypes.Contains(t))
            {
                return Result<KnowledgeEntityDocsModel>.Fail("Loại đối tượng phải là " + string.Join(" / ", DocEntityTypes));
            }
            var codes = await DocCodesAsync(t, id);
            var caller = WebCaller();
            var rights = await RightsAsync(caller);
            var (folder, types) = UploadTarget(t);
            var model = new KnowledgeEntityDocsModel { Code = codes.FirstOrDefault(), UploadFolder = folder, UploadTypes = [.. types], CanUpload = codes.Count > 0 && caller.AgentCode == null && rights.CanEdit(folder) };
            if (codes.Count == 0)
            {
                return Result<KnowledgeEntityDocsModel>.Success(model, "Bản ghi chưa có mã để gắn tài liệu");
            }
            var keys = codes.Concat(codes.Select(FileSafe)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            model.ListDocument = [.. All().Where(n => n.Meta.ContainsKey("file") && rights.NoteVisible(n)
                    && (keys.Contains((n.Meta.GetValueOrDefault("doi_tuong") ?? string.Empty).Trim()) || n.Refs.Any(r => keys.Contains(r.Trim()))))
                .OrderBy(n => n.Status == "luu-tru").ThenByDescending(n => ParseDate(n.Meta.GetValueOrDefault("ngay")))
                .Select(n => new KnowledgeDocumentModel
                {
                    Path = n.Path, Title = n.Title, Folder = n.Folder, Type = n.Type, SoHieu = n.Meta.GetValueOrDefault("so_hieu"), Date = ParseDate(n.Meta.GetValueOrDefault("ngay")),
                    ExpiresOn = ParseDate(n.Meta.GetValueOrDefault("het_han")), FileName = n.Meta.GetValueOrDefault("ten_goc"), CanDownload = caller.AgentCode == null && rights.CanExport(n.Folder),
                    Status = n.Meta.GetValueOrDefault("status"),
                })];
            return Result<KnowledgeEntityDocsModel>.Success(model, "OK");
        }

        /// <summary>Tài liệu hợp đồng trong kho (NCC / lao động / khách) có het_han còn đúng số ngày nhắc → theo vùng. Không gồm tài liệu Lưu trữ.</summary>
        public Dictionary<string, List<string>> ExpiringDocuments(DateTime today, int[] daysLeft)
            => All().Where(n => n.Type.StartsWith("hop-dong", StringComparison.Ordinal) && n.Status != "luu-tru" && ParseDate(n.Meta.GetValueOrDefault("het_han")) is { } e && daysLeft.Contains((e - today).Days))
                .GroupBy(n => n.Folder).ToDictionary(g => g.Key, g => g.Select(n => $"{n.Title}{(n.Meta.GetValueOrDefault("so_hieu") is { Length: > 0 } s ? " (" + s + ")" : string.Empty)} – hết hạn {ParseDate(n.Meta.GetValueOrDefault("het_han")):dd/MM/yyyy}").ToList());
    }

    /// <summary>AI-22.5 (D-141 i): 06:30 – nhắc hợp đồng sắp hết hạn (khách: Contract.EffectiveTo; NCC / lao động: het_han trong kho) trước 30 / 7 / 1 ngày cho người phụ trách.</summary>
    public sealed class ContractExpiryNoticeJob(IKnowledgeService knowledge, IUnitOfWork unitOfWork, INotificationService notifications, IClock clock,
        Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.KnowledgeOptions> options) : IScheduledJob
    {
        public string Code => "ContractExpiryNotice";
        public string Name => "Nhắc hợp đồng sắp hết hạn";
        public string Description => "06:30 – hợp đồng khách (Hiệu lực) / NCC / lao động còn 30, 7, 1 ngày hết hạn → nhắc người phụ trách (Kinh doanh, Mua hàng, Nhân sự) qua bot.";
        public string DefaultAt => "06:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = clock.Today;
            int[] days = [.. new[] { Math.Max(1, options.Value.ContractExpiryNoticeDays), 7, 1 }.Distinct()];
            var dates = days.Select(d => today.AddDays(d)).ToList();
            var db = unitOfWork.DbContext;
            var core = await db.Contract.AsNoTracking().Where(c => !c.IsDeleted && c.Status == ContractStatus.Active && c.EffectiveTo != null && dates.Contains(c.EffectiveTo.Value.Date))
                .Select(c => new { c.ContractNumber, c.ContractNo, c.EffectiveTo, Customer = db.Customer.Where(k => k.Id == c.CustomerId).Select(k => k.Name).FirstOrDefault() }).ToListAsync(cancellationToken);
            int n = 0;
            if (core.Count > 0)
            {
                n += core.Count;
                await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.KnowledgeReviewDue, ["#/crm-contract"], new
                {
                    title = $"{core.Count} hợp đồng khách sắp hết hạn", lines = core.Take(10).Select(c => $"📄 {c.ContractNumber ?? c.ContractNo} · {c.Customer} – hết hạn {c.EffectiveTo:dd/MM/yyyy}").ToList(),
                }, $"CONTRACT_EXPIRY:core:{today:yyyyMMdd}", requireAdd: true);
            }
            foreach (var (folder, items) in knowledge.ExpiringDocuments(today, days))
            {
                n += items.Count;
                var f = KnowledgeVault.FolderByCode(folder)!;
                await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.KnowledgeReviewDue, ["#/" + f.Screen], new
                {
                    title = $"{items.Count} hợp đồng sắp hết hạn ({f.Dir})", lines = items.Take(10).Select(x => "📄 " + x).ToList(),
                }, $"CONTRACT_EXPIRY:{folder}:{today:yyyyMMdd}", requireAdd: true);
            }
            return new JobOutcome(JobRunStatus.Succeeded, n, now.Date, n == 0 ? "Không có hợp đồng tới mốc nhắc" : $"Nhắc {n} hợp đồng (mốc {string.Join(" / ", days)} ngày)");
        }
    }
}
