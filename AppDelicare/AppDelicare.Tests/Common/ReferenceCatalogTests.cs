using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// Lỗi UAT 2026-11-21 (rà soát danh mục): cột mã danh mục (ReferenceCatalogs) – Insert / Update chung chỉ nhận giá trị trong ReferenceLists,
    /// nhận nhãn → lưu mã, giá trị cũ ngoài danh mục giữ được khi không đổi; GetReferenceLists trả kèm cột dùng danh mục (giao diện vẽ ô chọn).
    /// </summary>
    public class ReferenceCatalogTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory = factory;

        [Fact]
        public async Task Generic_insert_update_accept_only_catalog_codes_and_keep_unchanged_legacy_value()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Warehouse/Insert", ("Name", "[DEMO] Kho RC1"), ("WarehouseType", "Kho tổng"), ("Status", "Active"))).Fail("Loại kho «Kho tổng» không có trong danh mục");
            (await c.PostFormAsync("/api/Warehouse/Insert", ("Name", "[DEMO] Kho RC2"), ("WarehouseType", "kho lạnh"), ("Status", "Active"))).Ok();
            (await _factory.QueryAsync(db => db.Warehouse.AsNoTracking().Where(w => w.Name == "[DEMO] Kho RC2").Select(w => w.WarehouseType).SingleAsync()))
                .Should().Be("COLD", "nhận nhãn, lưu mã của danh mục");
            (await c.PostFormAsync("/api/Supplier/Insert", ("Name", "[DEMO] NCC RC"), ("SupplierType", "RAU"), ("Status", "Active"))).Fail("Loại nhà cung cấp «RAU»");

            Guid legacy = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = legacy, Code = "KO-RC-OLD", Name = "[DEMO] Kho RC cũ", WarehouseType = "KHO_CU", Status = "Active" });
                return Task.CompletedTask;
            });
            (await c.PutFormAsync("/api/Warehouse/Update", ("Id", legacy), ("Code", "KO-RC-OLD"), ("Name", "[DEMO] Kho RC cũ (đổi tên)"), ("WarehouseType", "KHO_CU"), ("Status", "Active")))
                .Ok(); // giá trị cũ ngoài danh mục giữ được khi không đổi
            (await c.PutFormAsync("/api/Warehouse/Update", ("Id", legacy), ("Code", "KO-RC-OLD"), ("Name", "[DEMO] Kho RC cũ"), ("WarehouseType", "KHO_MOI"), ("Status", "Active")))
                .Fail("không có trong danh mục");
        }

        [Fact]
        public async Task Complaint_channel_and_category_come_from_catalog()
        {
            Guid customer = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KHB-RC", CIF = "KHB-RC", Name = "[DEMO] Khách RC", DisplayName = "[DEMO] Khách RC", CustomerType = "BUSINESS", Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", customer), ("Severity", "Medium"), ("SourceChannel", "Facebook"), ("Description", "[DEMO] Cơm nguội")))
                .Fail("Kênh tiếp nhận «Facebook» không có trong danh mục");
            (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", customer), ("Severity", "Medium"), ("SourceChannel", "ZALO"), ("CategoryCode", "món dở"), ("Description", "[DEMO] Cơm nguội")))
                .Fail("Phân loại phản ánh «món dở»");
            (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", customer), ("Severity", "Medium"), ("SourceChannel", "Điện thoại"), ("CategoryCode", "giao trễ"), ("Description", "[DEMO] Cơm nguội"))).Ok();
            var saved = await _factory.QueryAsync(db => db.CustomerComplaint.AsNoTracking().SingleAsync(x => x.CustomerId == customer));
            saved.SourceChannel.Should().Be("PHONE");
            saved.CategoryCode.Should().Be("LATE");
        }

        [Fact]
        public async Task Reference_lists_expose_columns_for_select_inputs()
        {
            using var c = _factory.CreateAuthorizedClient();
            var lists = (await c.GetResultAsync("/api/MetaData/GetReferenceLists")).ListData.EnumerateArray().ToList();
            var meal = lists.Single(x => x.GetProperty("name").GetString() == "MealType");
            meal.GetProperty("listColumn").EnumerateArray().Select(x => x.GetString()).Should()
                .Contain(["ContractMeal.MealType", "MenuLine.MealType", "Forecast.MealType", "PricePolicy.MealType"]);
            lists.Single(x => x.GetProperty("name").GetString() == "WarehouseType").GetProperty("listItem").EnumerateArray()
                .Select(x => x.GetProperty("code").GetString()).Should().Contain("KITCHEN");
        }
    }
}
