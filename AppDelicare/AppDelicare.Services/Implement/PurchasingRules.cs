using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T15 – quy tắc mua hàng / nhận hàng dùng chung (PurchaseRequestService, PurchaseOrderService, GoodsReceiptService, QcInspectionService).
    /// Lớp tĩnh (như <see cref="InventoryLotRules"/>) – đọc qua <see cref="DatabaseContext"/> của service gọi để dùng chung transaction.
    /// </summary>
    public static class PurchasingRules
    {
        private const string Inactive = "Inactive";

        public static string Qty(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        public static string Money(decimal value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);

        public static bool IsInactive(string? status) => string.Equals((status ?? string.Empty).Trim(), Inactive, StringComparison.OrdinalIgnoreCase);

        /// <summary>Nối ghi chú mới vào cuối (xuống dòng), bỏ qua chuỗi rỗng.</summary>
        public static string? AppendNote(string? current, string? add)
            => string.IsNullOrWhiteSpace(add) ? current : string.IsNullOrWhiteSpace(current) ? add.Trim() : $"{current}\n{add.Trim()}";

        /// <summary>
        /// Bảng giá NCC đang hiệu lực tại <paramref name="date"/> cho các nguyên liệu (dòng / NCC không Inactive, NCC chưa xóa), xếp theo
        /// ưu tiên (IsPreferred) → giá thấp → lead time ngắn – cùng thứ tự gợi ý NCC của MRP (T14, GAP-MRP-05).
        /// </summary>
        public static async Task<List<(SupplierMaterial Material, Supplier Supplier)>> SupplierOffersAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, DateTime date, Guid? supplierId = null)
        {
            DateTime day = date.Date;
            var rows = (await db.SupplierMaterial.AsNoTracking()
                    .Where(m => !m.IsDeleted && ingredientIds.Contains(m.IngredientId) && (supplierId == null || m.SupplierId == supplierId)
                        && (m.EffectiveFrom == null || m.EffectiveFrom <= day) && (m.EffectiveTo == null || m.EffectiveTo >= day))
                    .ToListAsync())
                .Where(m => !IsInactive(m.Status)).ToList();
            var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
            var suppliers = (await db.Supplier.AsNoTracking().Where(s => supplierIds.Contains(s.Id) && !s.IsDeleted).ToListAsync())
                .Where(s => !IsInactive(s.Status)).ToDictionary(s => s.Id);
            return rows.Where(r => suppliers.ContainsKey(r.SupplierId))
                .OrderByDescending(r => r.IsPreferred).ThenBy(r => r.UnitPrice == null).ThenBy(r => r.UnitPrice).ThenBy(r => r.LeadTimeDays ?? int.MaxValue)
                .Select(r => (r, suppliers[r.SupplierId])).ToList();
        }

        /// <summary>
        /// Số đã nhận theo dòng PO (GAP-PUR-01: PurchaseOrderLine không có cột đã nhận; GoodsReceiptLine không có cột dòng PO → khớp theo nguyên liệu,
        /// mỗi nguyên liệu 1 dòng PO): Received = Σ số đạt của phiếu đã ghi sổ; Pending = Σ số nhận của phiếu chưa ghi sổ (không tính phiếu hủy).
        /// </summary>
        public static async Task<Dictionary<Guid, (decimal Received, decimal Pending)>> ReceiptTotalsAsync(DatabaseContext db, Guid purchaseOrderId, Guid? excludeReceiptId = null)
        {
            var rows = await (from r in db.GoodsReceipt.AsNoTracking()
                              join l in db.GoodsReceiptLine.AsNoTracking() on r.Id equals l.GoodsReceiptId
                              where r.PurchaseOrderId == purchaseOrderId && !r.IsDeleted && !l.IsDeleted && r.Status != GoodsReceiptStatus.Cancelled
                                    && (excludeReceiptId == null || r.Id != excludeReceiptId)
                              select new { l.IngredientId, r.Status, l.ReceivedQuantity, l.AcceptedQuantity }).ToListAsync();
            return rows.GroupBy(r => r.IngredientId).ToDictionary(g => g.Key, g => (
                g.Where(x => x.Status == GoodsReceiptStatus.Posted).Sum(x => x.AcceptedQuantity),
                g.Where(x => x.Status != GoodsReceiptStatus.Posted).Sum(x => x.ReceivedQuantity)));
        }

        /// <summary>T15.1: ngưỡng nhiệt độ nhận theo kiểu bảo quản (<c>BusinessRules:ReceivingTemp</c>); null = không kiểm (DRY / AMBIENT / trống).</summary>
        public static ReceivingTempRule? ReceivingTempRuleOf(BusinessRulesOptions rules, string? storageType)
            => !string.IsNullOrWhiteSpace(storageType) && rules.ReceivingTemp.TryGetValue(storageType.Trim(), out var rule) ? rule : null;

        /// <summary>Nhiệt độ nhận tối đa để "đạt" (AcceptMax) – dùng cho TemperatureLog.MaxAllowedC / hiển thị; null = không kiểm.</summary>
        public static decimal? MaxReceivingTemp(BusinessRulesOptions rules, string? storageType) => ReceivingTempRuleOf(rules, storageType)?.AcceptMax;

        /// <summary>T15.1 (anh chốt 2026-11-16): vùng nhiệt độ nhận – xem <see cref="ReceivingTempZone"/>.</summary>
        public static string TempZone(ReceivingTempRule? rule, decimal? temperature)
            => rule == null ? ReceivingTempZone.NotChecked
                : temperature == null ? ReceivingTempZone.Missing
                : temperature <= rule.AcceptMax ? ReceivingTempZone.Accept
                : temperature <= rule.ConditionalMax ? ReceivingTempZone.Conditional
                : ReceivingTempZone.Reject;

        /// <summary>
        /// Kết quả gợi ý: hạn dùng trước ngày nhận hoặc nhiệt độ vùng loại → KHONG_DAT; còn lại DAT (vùng có điều kiện: DAT kèm lý do – người kiểm quyết định).
        /// </summary>
        public static string SuggestResult(string zone, DateTime? expiryDate, DateTime receiptDate)
            => (expiryDate != null && expiryDate.Value.Date < receiptDate.Date) || zone == ReceivingTempZone.Reject
                ? IncomingQcResult.KhongDat
                : IncomingQcResult.Dat;

        /// <summary>Ghi chú "nhận có điều kiện – ưu tiên dùng trước" (QCInspectionLine.Note / MaterialLot.Note – GAP-PUR-10).</summary>
        public static string ConditionalNote(ReceivingTempRule rule, decimal temperature, bool noThawConfirmed)
            => $"{ReceivingCode.UseFirstMarker} Nhận có điều kiện: nhiệt độ {Qty(temperature)} °C (đạt ≤ {Qty(rule.AcceptMax)} °C, có điều kiện ≤ {Qty(rule.ConditionalMax)} °C) – ưu tiên dùng trước"
                + (noThawConfirmed ? "; đã xác nhận không có dấu hiệu rã đông" : string.Empty);

        /// <summary>
        /// T15.1 (phương án A – GAP-PUR-09): % VAT của từng nguyên liệu theo <c>BusinessRules:PurchaseVat</c> – mã nhóm nguyên liệu (IngredientCategoryId,
        /// cột cũ CategoryId), không khớp thì xét nhóm cha (tối đa 5 cấp), cuối cùng DefaultPct. Chỉ tính khi đọc, không lưu.
        /// </summary>
        public static async Task<Dictionary<Guid, decimal>> VatPctByIngredientAsync(DatabaseContext db, BusinessRulesOptions rules, IReadOnlyCollection<Guid> ingredientIds)
        {
            var vat = rules.PurchaseVat ?? new PurchaseVatOptions();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id))
                .Select(i => new { i.Id, CategoryId = i.IngredientCategoryId ?? i.CategoryId }).ToListAsync();
            var categories = vat.ByIngredientCategoryCode.Count == 0
                ? []
                : await db.IngredientCategory.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Code, c.ParentId }).ToDictionaryAsync(c => c.Id);
            var result = new Dictionary<Guid, decimal>();
            foreach (var i in ingredients)
            {
                decimal pct = vat.DefaultPct;
                Guid? current = i.CategoryId;
                for (int depth = 0; depth < 5 && current is { } cid && categories.TryGetValue(cid, out var category); depth++)
                {
                    if (!string.IsNullOrWhiteSpace(category.Code) && vat.ByIngredientCategoryCode.TryGetValue(category.Code.Trim(), out var byCode))
                    {
                        pct = byCode;
                        break;
                    }
                    current = category.ParentId;
                }
                result[i.Id] = pct;
            }
            return result;
        }

        /// <summary>Tiền VAT của 1 dòng: thành tiền × % / 100, làm tròn đồng (VND, 0,5 làm tròn lên).</summary>
        public static decimal VatAmount(decimal lineAmount, decimal pct) => Math.Round(lineAmount * pct / 100m, 0, MidpointRounding.AwayFromZero);
    }
}
