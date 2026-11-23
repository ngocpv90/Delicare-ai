using AppDelicare.Data.Connection;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// MENU-4.7 trên SQL Server dev (chỉ đọc, bỏ qua nếu không có DB): lọc Đề nghị mua / Đơn mua / Phiếu nhập / Phiếu xuất theo nguyên liệu qua GetAll có sẵn
    /// (thủ tục uspX_GetAll không đổi) – trả đúng các chứng từ có dòng chứa nguyên liệu, kèm tổng số lượng; phân trang lại theo số chứng từ khớp.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class Menu4SqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Document_lists_filter_by_ingredient_on_sql_server()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();
            int checkedLists = 0;

            async Task Check<T>(IQueryable<(Guid Doc, Guid Ingredient, decimal Qty)> lines, Func<Guid, Task<(List<Guid> Ids, List<decimal?> Qty, long Total)>> query)
            {
                var all = await lines.ToListAsync();
                var top = all.GroupBy(l => l.Ingredient).OrderByDescending(g => g.Select(x => x.Doc).Distinct().Count()).FirstOrDefault();
                if (top == null)
                {
                    return; // DB dev chưa có chứng từ loại này
                }
                var expected = top.GroupBy(l => l.Doc).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
                var (ids, qty, total) = await query(top.Key);
                total.Should().Be(expected.Count, typeof(T).Name);
                ids.Should().OnlyContain(id => expected.ContainsKey(id));
                for (int i = 0; i < ids.Count; i++)
                {
                    qty[i].Should().Be(expected[ids[i]]);
                }
                checkedLists++;
            }

            await Check<PurchaseOrderSearchRequest>(db.PurchaseOrderLine.Where(l => !l.IsDeleted && db.PurchaseOrder.Any(p => p.Id == l.PurchaseOrderId && !p.IsDeleted)).Select(l => ValueTuple.Create(l.PurchaseOrderId, l.IngredientId, l.Quantity)),
                async id => { var r = await sp.GetRequiredService<IPurchaseOrderService>().GetAll(new PurchaseOrderSearchRequest { IngredientId = id, PageSize = 500 }); return (r.ListData!.Select(x => x.Id).ToList(), r.ListData!.Select(x => x.IngredientQuantity).ToList(), r.TotalRecords); });
            await Check<PurchaseRequestSearchRequest>(db.PurchaseRequestLine.Where(l => !l.IsDeleted && db.PurchaseRequest.Any(p => p.Id == l.PurchaseRequestId && !p.IsDeleted)).Select(l => ValueTuple.Create(l.PurchaseRequestId, l.IngredientId, l.Quantity)),
                async id => { var r = await sp.GetRequiredService<IPurchaseRequestService>().GetAll(new PurchaseRequestSearchRequest { IngredientId = id, PageSize = 500 }); return (r.ListData!.Select(x => x.Id).ToList(), r.ListData!.Select(x => x.IngredientQuantity).ToList(), r.TotalRecords); });
            await Check<GoodsReceiptSearchRequest>(db.GoodsReceiptLine.Where(l => !l.IsDeleted && db.GoodsReceipt.Any(p => p.Id == l.GoodsReceiptId && !p.IsDeleted)).Select(l => ValueTuple.Create(l.GoodsReceiptId, l.IngredientId, l.ReceivedQuantity)),
                async id => { var r = await sp.GetRequiredService<IGoodsReceiptService>().GetAll(new GoodsReceiptSearchRequest { IngredientId = id, PageSize = 500 }); return (r.ListData!.Select(x => x.Id).ToList(), r.ListData!.Select(x => x.IngredientQuantity).ToList(), r.TotalRecords); });
            await Check<MaterialIssueSearchRequest>(db.MaterialIssueLine.Where(l => !l.IsDeleted && db.MaterialIssue.Any(p => p.Id == l.MaterialIssueId && !p.IsDeleted)).Select(l => ValueTuple.Create(l.MaterialIssueId, l.IngredientId, l.Quantity)),
                async id => { var r = await sp.GetRequiredService<IMaterialIssueService>().GetAll(new MaterialIssueSearchRequest { IngredientId = id, PageSize = 500 }); return (r.ListData!.Select(x => x.Id).ToList(), r.ListData!.Select(x => x.IngredientQuantity).ToList(), r.TotalRecords); });

            Skip.If(checkedLists == 0, "DB dev chưa có chứng từ mua / kho nào");
        }
    }
}
