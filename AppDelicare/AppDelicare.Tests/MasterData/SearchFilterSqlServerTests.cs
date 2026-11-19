using AppDelicare.Data.Connection;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.MasterData
{
    /// <summary>
    /// FE-2 follow-up trên SQL Server dev (bỏ qua nếu không có DB, chỉ đọc): bỏ trống lọc cha (CustomerId, WarehouseId…) = không lọc
    /// (trước đây bind thành Guid rỗng → danh sách rỗng); uspSupplier_GetAll / uspUnitMeasure_GetAll trả đủ cột và lọc Status.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class SearchFilterSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Omitted_parent_filters_return_all_rows_and_supplier_unit_lists_have_full_columns()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();

            var contacts = await sp.GetRequiredService<ICustomerContactService>().GetAll(new CustomerContactSearchRequest { PageSize = 1000 });
            contacts.TotalRecords.Should().Be(await db.CustomerContact.CountAsync(x => !x.IsDeleted));
            var zones = await sp.GetRequiredService<IStorageZoneService>().GetAll(new StorageZoneSearchRequest { PageSize = 1000 });
            zones.TotalRecords.Should().Be(await db.StorageZone.CountAsync(x => !x.IsDeleted));
            var materials = await sp.GetRequiredService<ISupplierMaterialService>().GetAll(new SupplierMaterialSearchRequest { PageSize = 1000, IngredientId = Guid.Empty });
            materials.TotalRecords.Should().Be(await db.SupplierMaterial.CountAsync(x => !x.IsDeleted), "Guid rỗng cũng coi như không lọc");

            var suppliers = await sp.GetRequiredService<ISupplierService>().GetAll(new SupplierSearchRequest { PageSize = 1000 });
            suppliers.TotalRecords.Should().Be(await db.Supplier.CountAsync(x => !x.IsDeleted));
            var withCode = await db.Supplier.Where(x => !x.IsDeleted && x.Code != null).Select(x => new { x.Id, x.Code, x.Status }).FirstOrDefaultAsync();
            if (withCode != null)
            {
                suppliers.ListData.Single(x => x.Id == withCode.Id).Code.Should().Be(withCode.Code);
                if (withCode.Status != null)
                {
                    var byStatus = await sp.GetRequiredService<ISupplierService>().GetAll(new SupplierSearchRequest { PageSize = 1000, Status = withCode.Status });
                    byStatus.ListData.Should().OnlyContain(x => x.Status == withCode.Status);
                    byStatus.TotalRecords.Should().Be(await db.Supplier.CountAsync(x => !x.IsDeleted && x.Status == withCode.Status));
                }
            }

            var units = await sp.GetRequiredService<IUnitMeasureService>().GetAll(new UnitMeasureSearchRequest { PageSize = 1000 });
            units.TotalRecords.Should().Be(await db.UnitMeasure.CountAsync(x => !x.IsDeleted));
            var unit = await db.UnitMeasure.Where(x => !x.IsDeleted && x.Code != null).Select(x => new { x.Id, x.Code }).FirstOrDefaultAsync();
            if (unit != null)
            {
                units.ListData.Single(x => x.Id == unit.Id).Code.Should().Be(unit.Code);
            }
        }

        /// <summary>
        /// T50R1 (lỗi tìm thấy khi chạy luồng Pilot bằng API): 35 SearchRequest còn lọc cha kiểu <c>Guid</c> không null (MaterialLot.IngredientId,
        /// Order.CustomerId, Recipe.DishId, ProductionBatch.ProductionOrderId…) → bỏ trống bị bind thành Guid rỗng → GetAll luôn rỗng
        /// (vd. màn Lô nguyên liệu, Đơn hàng, Mẻ sản xuất không có dòng nào). Nay mọi lọc Id là <c>Guid?</c>, service đổi Guid rỗng → NULL.
        /// </summary>
        [SkippableFact]
        public async Task Pilot_lists_without_parent_filter_return_all_rows()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();

            (await sp.GetRequiredService<IMaterialLotService>().GetAll(new MaterialLotSearchRequest { PageSize = 1 })).TotalRecords
                .Should().Be(await db.MaterialLot.CountAsync(x => !x.IsDeleted));
            (await sp.GetRequiredService<IMaterialLotService>().GetAll(new MaterialLotSearchRequest { PageSize = 1, IngredientId = Guid.Empty })).TotalRecords
                .Should().Be(await db.MaterialLot.CountAsync(x => !x.IsDeleted), "Guid rỗng cũng coi như không lọc");
            (await sp.GetRequiredService<IRecipeService>().GetAll(new RecipeSearchRequest { PageSize = 1 })).TotalRecords
                .Should().Be(await db.Recipe.CountAsync(x => !x.IsDeleted));
            (await sp.GetRequiredService<IOrderService>().GetAll(new OrderSearchRequest { PageSize = 1 })).TotalRecords
                .Should().Be(await db.Order.CountAsync(x => !x.IsDeleted));
            (await sp.GetRequiredService<IProductionBatchService>().GetAll(new ProductionBatchSearchRequest { PageSize = 1 })).TotalRecords
                .Should().Be(await db.ProductionBatch.CountAsync(x => !x.IsDeleted));
            (await sp.GetRequiredService<IMaterialIssueService>().GetAll(new MaterialIssueSearchRequest { PageSize = 1 })).TotalRecords
                .Should().Be(await db.MaterialIssue.CountAsync(x => !x.IsDeleted));
            (await sp.GetRequiredService<IMenuAssignmentService>().GetAll(new MenuAssignmentSearchRequest { PageSize = 1 })).TotalRecords
                .Should().Be(await db.MenuAssignment.CountAsync(x => !x.IsDeleted));
        }

        /// <summary>UI-2 (D-66 f3): Order/GetAll lọc ngày ăn từ – đến (gồm 2 đầu) trên SQL Server dev – chỉ đọc.</summary>
        [SkippableFact]
        public async Task Order_list_filters_by_meal_date_range()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();
            var day = await db.Order.Where(x => !x.IsDeleted).Select(x => x.MealDate).OrderByDescending(x => x).FirstOrDefaultAsync();
            Skip.If(day == default, "DB dev chưa có đơn");
            DateTime from = day.Date.AddDays(-2), to = day.Date;
            int expected = await db.Order.CountAsync(x => !x.IsDeleted && x.MealDate >= from && x.MealDate < to.AddDays(1));
            (await sp.GetRequiredService<IOrderService>().GetAll(new OrderSearchRequest { PageSize = 1, FromMealDate = from, ToMealDate = to })).TotalRecords
                .Should().Be(expected);
        }

        /// <summary>T50R1: chặn tái phát – mọi thuộc tính lọc *Id kiểu Guid trong SearchRequest phải là Guid? (bỏ trống = không lọc).</summary>
        [Fact]
        public void Every_guid_filter_of_search_requests_is_nullable()
        {
            var offenders = typeof(BaseSearchRequest).Assembly.GetTypes()
                .Where(t => t.IsClass && typeof(BaseSearchRequest).IsAssignableFrom(t))
                .SelectMany(t => t.GetProperties().Where(p => p.PropertyType == typeof(Guid)).Select(p => $"{t.Name}.{p.Name}"))
                .ToList();
            offenders.Should().BeEmpty("lọc Guid không null bị bind thành Guid rỗng khi bỏ trống → danh sách rỗng");
        }
    }
}
