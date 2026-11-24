using System.Net.Http.Headers;
using System.Text;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// UI-6.4 (D-83): phụ lục điều chỉnh giá trong hợp đồng – luật lập (hợp đồng Hiệu lực, từ ≥ ngày mai, trong hiệu lực, không chồng thời gian),
    /// gửi duyệt bắt buộc có file phụ lục đã ký, duyệt qua engine → Hiệu lực; tra giá 2 cấp (phụ lục đang hiệu lực → giá gốc hợp đồng); dữ liệu cũ theo khách vẫn đọc.
    /// </summary>
    public sealed class PriceAdjustmentTests : IDisposable
    {
        private readonly InMemoryApiFactory _factory = new();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "delicare-ui64-" + Guid.NewGuid().ToString("N")[..8]);
        private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%phu luc\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");

        public PriceAdjustmentTests()
        {
            _factory.Settings["DocumentStorage:RootPath"] = _root;
        }

        public void Dispose()
        {
            _factory.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private sealed record Scenario(Guid Customer, Guid Contract);

        private async Task<Scenario> SeedAsync(string code, string status = "Active")
        {
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "BUSINESS", DisplayName = code, Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = status, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2027, 12, 31) });
                db.ContractMeal.AddRange(
                    new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 },
                    new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TOI", MealName = "Suất tối", UnitPrice = 35000 });
                return Task.CompletedTask;
            });
            return new Scenario(customer, contract);
        }

        private static StringContent Json(string s) => new(s, Encoding.UTF8, "application/json");

        private static MultipartFormDataContent PdfForm(Guid recordId)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(Pdf);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "File", "phu-luc-da-ky.pdf");
            form.Add(new StringContent("other"), "Category");
            form.Add(new StringContent("PricePolicy"), "Resource");
            form.Add(new StringContent(recordId.ToString()), "RecordId");
            return form;
        }

        [Fact]
        public async Task Create_submit_requires_file_approve_then_resolve_uses_appendix_before_base_price()
        {
            var s = await SeedAsync("PA1");
            using var c = _factory.CreateAuthorizedClient();
            string from = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd");

            // Luật lập
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{s.Contract}}","effectiveFrom":"{{DateTime.Today:yyyy-MM-dd}}","listLine":[{"mealType":"TRUA","unitPrice":42000}]}""")))).Fail("ngày mai");
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{s.Contract}}","effectiveFrom":"{{from}}","listLine":[{"mealType":"TRUA","unitPrice":0}]}""")))).Fail("lớn hơn 0");
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{s.Contract}}","effectiveFrom":"{{from}}","listLine":[{"mealType":"SANG","unitPrice":20000}]}""")))).Fail("không có trong hợp đồng");

            var created = (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{s.Contract}}","effectiveFrom":"{{from}}","reason":"Tăng giá nguyên liệu","listLine":[{"mealType":"TRUA","unitPrice":44000}]}""")))).Ok();
            Guid id = created.EnumerateArray().Single().GetGuid();

            // Chồng thời gian cùng loại suất
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{s.Contract}}","effectiveFrom":"{{DateTime.Today.AddDays(20):yyyy-MM-dd}}","listLine":[{"mealType":"TRUA","unitPrice":45000}]}""")))).Fail("chồng thời gian");

            // Gửi duyệt khi chưa có file → chặn; có file → Chờ duyệt + yêu cầu duyệt
            (await c.PostFormAsync("/api/Contract/SubmitPriceAdjustment", ("Id", id))).Fail("phụ lục đã ký");
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/DocumentFile/Upload", PdfForm(id)))).Ok();
            (await c.PostFormAsync("/api/Contract/SubmitPriceAdjustment", ("Id", id))).Ok();
            var req = await _factory.QueryAsync(db => db.ApprovalRequest.SingleAsync(a => a.BusinessId == id && a.BusinessType == "PRICE_ADJUSTMENT"));
            (await _factory.QueryAsync(db => db.PricePolicy.Where(p => p.Id == id).Select(p => p.Status).SingleAsync())).Should().Be("PendingApproval");

            // Tóm tắt duyệt: giá cũ → giá mới, chênh %
            var detail = (await c.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={req.Id}")).Ok();
            var sum = detail.GetProperty("summary");
            sum.GetProperty("typeName").GetString().Should().Be("Phụ lục điều chỉnh giá");
            sum.GetProperty("mainValue").GetString().Should().Contain("40.000").And.Contain("44.000").And.Contain("+10");

            // Duyệt (admin) → Hiệu lực
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", req.Id), ("Approve", true), ("Note", "Đồng ý"))).Ok();
            (await _factory.QueryAsync(db => db.PricePolicy.Where(p => p.Id == id).Select(p => p.Status).SingleAsync())).Should().Be("Active");

            // Tra giá: trước ngày áp → giá gốc; từ ngày áp → phụ lục; loại suất khác → giá gốc
            (await c.GetResultAsync($"/api/PricePolicy/Resolve?customerId={s.Customer}&contractId={s.Contract}&mealType=TRUA&date={DateTime.Today.AddDays(2):yyyy-MM-dd}")).Ok()
                .GetProperty("unitPrice").GetDecimal().Should().Be(40000);
            var after = (await c.GetResultAsync($"/api/PricePolicy/Resolve?customerId={s.Customer}&contractId={s.Contract}&mealType=TRUA&date={from}")).Ok();
            after.GetProperty("unitPrice").GetDecimal().Should().Be(44000);
            after.GetProperty("source").GetString().Should().Be("PricePolicy:Contract");
            (await c.GetResultAsync($"/api/PricePolicy/Resolve?customerId={s.Customer}&contractId={s.Contract}&mealType=TOI&date={from}")).Ok()
                .GetProperty("unitPrice").GetDecimal().Should().Be(35000);

            // Tab giá: giá gốc + lịch sử (chênh %, người duyệt, có file)
            var prices = (await c.GetResultAsync($"/api/Contract/GetPrices?id={s.Contract}")).Ok();
            var adj = prices.GetProperty("listAdjustment").EnumerateArray().Single();
            adj.GetProperty("status").GetString().Should().Be("Active");
            adj.GetProperty("changePct").GetDecimal().Should().Be(10m);
            adj.GetProperty("fileCount").GetInt32().Should().Be(1);
            adj.GetProperty("approvedBy").GetString().Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Only_active_contracts_can_be_adjusted_and_drafts_can_be_deleted()
        {
            var draft = await SeedAsync("PA2", "Draft");
            using var c = _factory.CreateAuthorizedClient();
            string from = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd");
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{draft.Contract}}","effectiveFrom":"{{from}}","listLine":[{"mealType":"TRUA","unitPrice":41000}]}""")))).Fail("Hiệu lực");

            var s = await SeedAsync("PA3");
            var ids = (await LegacyResult.ReadAsync(await c.PostAsync("/api/Contract/CreatePriceAdjustment",
                Json($$"""{"contractId":"{{s.Contract}}","effectiveFrom":"{{from}}","listLine":[{"mealType":"TRUA","unitPrice":41000},{"mealType":"TOI","unitPrice":36000}]}""")))).Ok()
                .EnumerateArray().Select(x => x.GetGuid()).ToList();
            ids.Should().HaveCount(2, "một phụ lục nhiều loại suất = mỗi loại suất 1 dòng");
            (await c.PostFormAsync("/api/Contract/DeletePriceAdjustment", ("Id", ids[0]))).Ok();
            (await _factory.QueryAsync(db => db.PricePolicy.CountAsync(p => p.ContractId == s.Contract && !p.IsDeleted))).Should().Be(1);
        }

        [Fact]
        public async Task Legacy_customer_level_policy_is_still_read_until_migrated()
        {
            var s = await SeedAsync("PA4");
            await _factory.SeedAsync(db =>
            {
                db.PricePolicy.Add(new PricePolicy { Id = Guid.NewGuid(), CustomerId = s.Customer, ContractId = null, MealType = "TRUA", UnitPrice = 39000, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1), Code = "CS-PA4" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var r = (await c.GetResultAsync($"/api/PricePolicy/Resolve?customerId={s.Customer}&contractId={s.Contract}&mealType=TRUA&date={DateTime.Today.AddDays(2):yyyy-MM-dd}")).Ok();
            r.GetProperty("unitPrice").GetDecimal().Should().Be(39000, "khách chưa chuyển sang phụ lục hợp đồng – vẫn đọc bản theo khách (D-83 e)");
            r.GetProperty("source").GetString().Should().Be("PricePolicy:Customer");
        }
    }
}
