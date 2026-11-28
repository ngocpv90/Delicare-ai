using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Knowledge
{
    public sealed class KnowledgeEntityNoteModel
    {
        public KnowledgeNoteHeadModel Note { get; set; } = new();
        /// <summary>true = mã nằm trong <c>refs</c> của frontmatter; false = chỉ được nhắc trong nội dung.</summary>
        public bool ByRef { get; set; }
        public string? Snippet { get; set; }
    }

    /// <summary>AI-17 (D-133): ghi chú kho tri thức gắn với 1 đối tượng nghiệp vụ – tab "Tri thức" trên trang chi tiết (khuôn D-80).</summary>
    public partial class KnowledgeService
    {
        public static readonly string[] EntityTypes = ["customer", "supplier", "dish", "lot", "batch", "route"];

        private async Task<List<string>> EntityCodesAsync(string type, Guid id)
        {
            var db = unitOfWork.DbContext;
            IEnumerable<string?> codes = type switch
            {
                "customer" => await db.Customer.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.Code, x.CIF }).FirstOrDefaultAsync() ?? [],
                "supplier" => await db.Supplier.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.Code }).FirstOrDefaultAsync() ?? [],
                "dish" => await db.Dish.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.Code }).FirstOrDefaultAsync() ?? [],
                "lot" => await db.MaterialLot.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.LotNumber, x.LotNo }).FirstOrDefaultAsync() ?? [],
                "batch" => await db.ProductionBatch.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.BatchNumber, x.BatchNo }).FirstOrDefaultAsync() ?? [],
                "route" => await db.DeliveryRoute.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted).Select(x => new[] { x.RouteNo, x.Code }).FirstOrDefaultAsync() ?? [],
                _ => [],
            };
            // Mã quá ngắn (≤ 2 ký tự) dễ trùng chữ thường – bỏ.
            return [.. codes.Where(c => !string.IsNullOrWhiteSpace(c) && c.Trim().Length > 2).Select(c => c!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        public async Task<IResults<KnowledgeEntityNoteModel>> GetByEntity(string? type, Guid id)
        {
            string t = (type ?? string.Empty).Trim().ToLowerInvariant();
            if (!EntityTypes.Contains(t))
            {
                return StatusList<KnowledgeEntityNoteModel>(400, "Loại đối tượng phải là " + string.Join(" / ", EntityTypes));
            }
            var codes = await EntityCodesAsync(t, id);
            if (codes.Count == 0)
            {
                return Results<KnowledgeEntityNoteModel>.Success([], "Bản ghi không có mã để tra kho tri thức");
            }
            var rights = await RightsAsync(WebCaller());
            var refKeys = codes.Select(c => KnowledgeVault.FileSafe(c)).Concat(codes).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var list = new List<KnowledgeEntityNoteModel>();
            foreach (var n in All().Where(rights.NoteVisible))
            {
                bool byRef = n.Refs.Any(r => refKeys.Contains(r.Trim()));
                string? hit = byRef ? null : codes.FirstOrDefault(c => n.Body.Contains(c, StringComparison.OrdinalIgnoreCase));
                if (!byRef && hit == null)
                {
                    continue;
                }
                string? snippet = null;
                string probe = hit ?? codes.FirstOrDefault(c => n.Body.Contains(c, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                int at = probe.Length == 0 ? -1 : n.Body.IndexOf(probe, StringComparison.OrdinalIgnoreCase);
                if (at >= 0)
                {
                    int from = Math.Max(0, at - 60), to = Math.Min(n.Body.Length, at + probe.Length + 100);
                    snippet = (from > 0 ? "…" : string.Empty) + System.Text.RegularExpressions.Regex.Replace(n.Body[from..to], @"(^|\n)#+\s*", "$1").Replace('\n', ' ').Trim() + (to < n.Body.Length ? "…" : string.Empty);
                }
                list.Add(new KnowledgeEntityNoteModel { Note = Head(n), ByRef = byRef, Snippet = snippet });
            }
            // Gắn refs trước; ghi chú hiệu lực trước nháp / lưu trữ; mới trước.
            var ordered = list.OrderByDescending(x => x.ByRef).ThenBy(x => x.Note.Status == "hiệu lực" || KnowledgeVault.Slug(x.Note.Status) == "hieu-luc" ? 0 : 1)
                .ThenByDescending(x => x.Note.ValidFrom).Take(100).ToList();
            return Results<KnowledgeEntityNoteModel>.Success(ordered, "OK");
        }
    }
}
