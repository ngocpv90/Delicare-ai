using System.Globalization;
using System.Net.Http.Headers;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// FE-2 follow-up: form (<c>[FromForm]</c>) đọc số thập phân theo InvariantCulture – máy chủ chạy văn hóa vi-VN ("," là dấu thập phân,
    /// "." là dấu nhóm nghìn) không được hiểu "12.5" thành 125. Giả lập máy chủ vi-VN bằng middleware đặt CurrentCulture cho mọi request.
    /// </summary>
    public class FormCultureTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public FormCultureTests(InMemoryApiFactory factory) => _factory = factory;

        private sealed class VietnameseServerCulture : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
                    CultureInfo.CurrentUICulture = new CultureInfo("vi-VN");
                    return nextMiddleware(context);
                });
                next(app);
            };
        }

        [Fact]
        public async Task Form_decimal_with_dot_is_parsed_invariantly_on_vietnamese_server_culture()
        {
            decimal.Parse("12.5", new CultureInfo("vi-VN")).Should().Be(125, "tiền đề: vi-VN đọc '.' là dấu nhóm nghìn");
            Guid ingredient = Guid.NewGuid(), warehouse = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "CULT-1", Name = "Bột culture" });
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "CULT-W", Name = "Kho culture", Status = "Active" });
                return Task.CompletedTask;
            });
            using var _ = _factory.CreateAuthorizedClient(); // tạo user admin của test
            using var vi = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddTransient<IStartupFilter, VietnameseServerCulture>()));
            using var c = vi.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InMemoryApiFactory.CreateToken(InMemoryApiFactory.UserId));

            var response = await c.PostAsync("/api/InventoryPolicy/Insert", new FormUrlEncodedContent(
            [
                new("IngredientId", ingredient.ToString()), new("WarehouseId", warehouse.ToString()),
                new("SafetyStock", "12.5"), new("MinimumStock", "0.25"), new("Status", "Active"),
            ]));
            (await LegacyResult.ReadAsync(response)).Ok();

            var policy = await vi.Services.CreateScope().ServiceProvider.GetRequiredService<AppDelicare.Data.Connection.DatabaseContext>()
                .InventoryPolicy.AsNoTracking().SingleAsync(p => p.IngredientId == ingredient);
            policy.SafetyStock.Should().Be(12.5m);
            policy.MinimumStock.Should().Be(0.25m);
        }
    }
}
