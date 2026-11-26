using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    public partial class AccountingVoucherService
    {
        /// <summary>
        /// ACC-5 (D-131 i): sổ kho RETURN của phiếu trả NCC (ReferenceType <c>SupplierReturn</c>) → chứng từ "Trả lại hàng mua" (tab Mua hàng, đối tượng = mã NCC,
        /// giá = giá lô). Trả về các dòng sổ còn lại cho nhóm "Kho khác".
        /// </summary>
        private async Task<List<InventoryTransaction>> PurchaseReturnsAsync(List<InventoryTransaction> other, List<VoucherModel> list,
            Dictionary<Guid, (string? Code, string? Name)> ingredients, Func<Guid, string> unitOf)
        {
            var mine = other.Where(t => t.TransactionType == "RETURN" && t.ReferenceType == SupplierReturnService.ReferenceType && t.ReferenceId != null).ToList();
            if (mine.Count == 0)
            {
                return other;
            }
            var db = _unitOfWork.DbContext;
            var ids = mine.Select(t => t.ReferenceId!.Value).Distinct().ToList();
            var issues = await db.SupplierIssue.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var supIds = issues.Values.Select(i => i.SupplierId).Distinct().ToList();
            var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name });
            var grIds = issues.Values.Where(i => i.GoodsReceiptId != null).Select(i => i.GoodsReceiptId!.Value).Distinct().ToList();
            var grs = await db.GoodsReceipt.AsNoTracking().Where(r => grIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.ReceiptNumber ?? r.ReceiptNo);
            foreach (var g in mine.GroupBy(t => t.ReferenceId!.Value))
            {
                var issue = issues.GetValueOrDefault(g.Key);
                var s = issue == null ? null : sups.GetValueOrDefault(issue.SupplierId);
                var first = g.OrderBy(t => t.TransactionDate).First();
                string? gr = issue?.GoodsReceiptId is { } r ? grs.GetValueOrDefault(r) : null;
                var v = new VoucherModel
                {
                    Key = $"{VoucherType.PurchaseReturn}|{g.Key:N}", Type = VoucherType.PurchaseReturn, Number = first.TransactionNumber ?? (issue != null ? SupplierReturnService.NumberOf(issue) : g.Key.ToString("N")[..8]),
                    Date = first.TransactionDate.Date, ObjectCode = s?.Code, ObjectName = s?.Name, Source = "Phiếu trả hàng NCC đã ghi sổ" + (gr == null ? string.Empty : $" (nhập {gr})"),
                };
                foreach (var ig in g.GroupBy(t => t.IngredientId))
                {
                    decimal qty = ig.Sum(t => t.QuantityOut - t.QuantityIn);
                    if (qty == 0)
                    {
                        continue;
                    }
                    var ing = ingredients.GetValueOrDefault(ig.Key);
                    bool priced = ig.All(t => t.UnitCost != null);
                    decimal amount = ig.Sum(t => (t.QuantityOut - t.QuantityIn) * (t.UnitCost ?? 0m));
                    var u = ig.First();
                    v.ListLine.Add(new VoucherLineModel
                    {
                        Code = ing.Code, Name = ing.Name, Unit = unitOf(u.UnitId != Guid.Empty ? u.UnitId : u.UomId), Quantity = qty,
                        UnitPrice = priced ? Math.Round(amount / qty, 2) : null, Amount = priced ? Math.Round(amount, 0) : null, Target = "Trả lại hàng mua", Note = first.Reason,
                    });
                }
                if (v.ListLine.Count == 0)
                {
                    continue;
                }
                v.Amount = v.ListLine.Sum(x => x.Amount ?? 0m);
                v.Note = $"{v.ListLine.Count} dòng · trả {N(v.Amount ?? 0m)}" + (first.Reason == null ? string.Empty : $" · {first.Reason}");
                if (v.ListLine.Any(x => x.UnitPrice == null))
                {
                    (v.Status, v.Note, v.FixLink, v.FixLabel) = (VoucherStatus.Error, "Thiếu giá lô – không tính được giá trị trả", "#/scm-supplier-return", "Trả hàng NCC");
                }
                list.Add(v);
            }
            return [.. other.Where(t => !mine.Contains(t))];
        }
    }
}
