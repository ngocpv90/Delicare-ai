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

        /// <summary>Nhiệt độ nhận tối đa (°C) theo kiểu bảo quản (<c>BusinessRules:ReceivingTempMaxC</c>); null = không giới hạn (PROPOSED).</summary>
        public static decimal? MaxReceivingTemp(BusinessRulesOptions rules, string? storageType)
            => !string.IsNullOrWhiteSpace(storageType) && rules.ReceivingTempMaxC.TryGetValue(storageType.Trim(), out var max) ? max : null;

        /// <summary>Kết quả gợi ý: hạn dùng trước ngày nhận hoặc nhiệt độ vượt ngưỡng → KHONG_DAT, còn lại DAT (spec 9.1 – PROPOSED).</summary>
        public static string SuggestResult(decimal? temperature, decimal? maxTemp, DateTime? expiryDate, DateTime receiptDate)
            => (expiryDate != null && expiryDate.Value.Date < receiptDate.Date) || (temperature != null && maxTemp != null && temperature > maxTemp)
                ? IncomingQcResult.KhongDat
                : IncomingQcResult.Dat;
    }
}
