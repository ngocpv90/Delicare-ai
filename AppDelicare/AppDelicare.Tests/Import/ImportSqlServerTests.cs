using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Import
{
    /// <summary>
    /// T49 – trên SQL Server dev (bỏ qua nếu không có DB): dòng ví dụ [DEMO] của MỌI file mẫu nạp được vào DB thật
    /// (cột NOT NULL, độ dài, kiểu) – chạy trong transaction ngoài rồi ROLLBACK, không để lại dữ liệu.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ImportSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Every_template_sample_row_imports_on_sql_server()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var import = scope.ServiceProvider.GetRequiredService<IMasterDataImportService>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int customersBefore = await db.Customer.CountAsync();

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                // Thứ tự theo phụ thuộc: khách → điểm giao; NL + món → định mức; NCC + NL → bảng giá.
                foreach (string table in new[] { "UnitMeasure", "Customer", "CustomerDeliveryLocation", "Supplier", "Ingredient", "Warehouse", "Vehicle", "Employee", "Dish", "RecipeLine", "SupplierMaterial" })
                {
                    var bytes = import.BuildTemplate(table)!;
                    var result = await import.Import(table, new MemoryStream(bytes));
                    result.ResponseCode.Should().Be(200, $"{table}: {result.ResponseMessage}");
                    result.Data!.ListError.Should().BeEmpty($"{table}: {string.Join("; ", result.Data.ListError.Select(e => e.Row + ": " + e.Message))}");
                    (result.Data.Inserted + result.Data.Updated + result.Data.Skipped).Should().BeGreaterThan(0, table);
                }

                db.ChangeTracker.Clear();
                (await db.Customer.AsNoTracking().SingleAsync(x => x.Code == "[DEMO]-KH01")).CIF.Should().Be("[DEMO]-KH01");
                (await db.RecipeLine.AsNoTracking().CountAsync(l => db.Recipe.Any(r => r.Id == l.RecipeId && db.Dish.Any(d => d.Id == r.DishId && d.Code == "[DEMO]-MON01")))).Should().Be(1);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.Customer.CountAsync()).Should().Be(customersBefore, "rollback không để lại dữ liệu");
        }
    }
}
