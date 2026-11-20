using AppDelicare.Model.Helper;
using FluentAssertions;

namespace AppDelicare.Tests.Common
{
    /// <summary>UG-1 C-10: danh mục trạng thái hiệu lực lấy từ các lớp *Status.cs (mã + nhãn) – khớp những gì máy chủ thực sự ghi.</summary>
    public class StatusCatalogEffectiveTests
    {
        private static StatusCatalog.Item Set(string table, string column) => StatusCatalogEffective.Find(table, column)!;

        [Fact]
        public void Columns_with_a_status_class_follow_the_code_codes_and_labels()
        {
            var po = Set("PurchaseOrder", "Status");
            po.Source.Should().Be(StatusCatalogEffective.CodeSource);
            po.Codes.Should().Contain(PurchaseOrderStatus.PendingApproval);
            po.Labels[Array.IndexOf(po.Codes, PurchaseOrderStatus.PendingApproval)].Should().Be(PurchaseOrderStatus.Label(PurchaseOrderStatus.PendingApproval));

            Set("GoodsReceipt", "Status").Codes.Should().NotContain("Draft", "máy chủ không có trạng thái Nháp cho phiếu nhập");
            Set("DeliveryOrder", "Status").Codes.Should().Contain(DeliveryOrderStatus.Planned);
            Set("CleaningBatche", "Status").Should().NotBeNull();

            var ex = Set("DeliveryExecution", "Status");
            foreach (string code in ex.Codes)
            {
                ex.Labels[Array.IndexOf(ex.Codes, code)].Should().Be(DeliveryExecutionStatus.Label(code));
            }
        }

        [Fact]
        public void Business_type_constants_are_not_listed_as_statuses_and_keys_are_unique()
        {
            Set("Order", "Status").Codes.Should().NotContain(OrderStatus.DeviationBusinessType).And.NotContain(OrderStatus.ChangeAfterCutoffBusinessType);
            StatusCatalogEffective.All.Select(x => x.Key).Should().OnlyHaveUniqueItems();
            StatusCatalogEffective.All.Should().OnlyContain(x => x.Codes.Length == x.Labels.Length && x.Codes.Length > 0);
        }
    }
}
