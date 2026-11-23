using AppDelicare.Services.Implement;
using FluentAssertions;

namespace AppDelicare.Tests.Sales
{
    /// <summary>BIZ-3.6 (D-77 – A10): ngưỡng thất thoát khay theo khách trong Customer.ExternalData – gộp khóa, không làm mất khóa khác.</summary>
    public class CustomerExternalDataTests
    {
        [Fact]
        public void Merge_keeps_other_keys_sets_and_removes_threshold()
        {
            CustomerExternalData.Merge("{\"zalo\":\"abc\"}", null).Should().Be("{\"zalo\":\"abc\"}", "trống = giữ nguyên");
            CustomerExternalData.Merge("{\"zalo\":\"abc\"}", "{\"containerLossMaxPct\":5}").Should().Be("{\"zalo\":\"abc\",\"containerLossMaxPct\":5}");
            CustomerExternalData.Merge("{\"zalo\":\"abc\",\"containerLossMaxPct\":5}", "{\"containerLossMaxPct\":null}").Should().Be("{\"zalo\":\"abc\"}");
            CustomerExternalData.Merge("{\"containerLossMaxPct\":5}", "{\"containerLossMaxPct\":null}").Should().BeNull();
            CustomerExternalData.Merge("ghi tay cũ", "{\"containerLossMaxPct\":2.5}").Should().Be("{\"containerLossMaxPct\":2.5}");
            CustomerExternalData.Validate("{\"containerLossMaxPct\":101}").Should().Contain("0 đến 100");
            CustomerExternalData.Validate("{\"containerLossMaxPct\":4.5}").Should().BeNull();
        }
    }
}
