using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// FIX-NOW-1 (D-92 b 5): quét giao diện bằng tài khoản vai trò trên DB dev thấy màn của vai trò gọi <c>GetAllData</c> của bảng liên quan bị 403
    /// (OPS-SCM mở Thực đơn tuần → nhóm món; OPS-WH mở Đơn mua → đề nghị mua, Giữ chỗ → đơn vị tính, Lệnh SX → món / công thức…).
    /// Nay <c>GetAllData</c> (chỉ danh sách tra cứu) mở thêm cho người Xem được màn có bảng liên quan; màn danh sách / ghi của bảng đó vẫn theo ma trận.
    /// </summary>
    public class LookupReadPermissionTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Theory]
        [InlineData("DishCategory", "menu-menu")]
        [InlineData("Dish", "menu-recipe")]
        [InlineData("Dish", "prod-plan")] // UI-6.24 (D-105): Lệnh sản xuất ẩn – quyền theo Kế hoạch sản xuất
        [InlineData("ProductionOrder", "scm-issue")]
        [InlineData("PurchaseRequest", "scm-po")]
        [InlineData("QcInspection", "scm-gr")]
        [InlineData("UnitMeasure", "scm-issue")] // UI-6.29 (D-110): Giữ chỗ gộp vào Xuất kho sản xuất
        [InlineData("User", "qc-supplier-issue")]
        [InlineData("Person", "int-identity")]
        [InlineData("DeliveryOrder", "log-return-inspection")]
        [InlineData("DeliveryStop", "log-route")] // UI-6.34: Đơn giao hàng gộp vào Điều phối
        [InlineData("MaterialReservation", "scm-issue")]
        [InlineData("Person", "qc-supplier-issue")]
        public void Lookup_screens_follow_logical_relations(string controller, string screen)
            => PermissionConvention.LookupScreensOf(controller).Should().Contain(screen);

        [Fact]
        public void Document_tables_are_not_opened_to_unrelated_screens()
        {
            PermissionConvention.LookupScreensOf("PurchaseOrder").Should().NotContain(["crm-customer", "hr-attendance", "log-complaint"]);
            PermissionConvention.LookupScreensOf("Employee").Should().NotContain("crm-contract");
        }

        private async Task<Guid> UserWithViewAsync(string link)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "lk-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "LOOKUP-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                if (db.Entry(menu).State == EntityState.Detached)
                {
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
            });
            return userId;
        }

        [Fact]
        public async Task Role_viewing_menu_week_reads_dish_categories_but_not_their_list_screen_or_writes()
        {
            using var c = factory.CreateAuthorizedClient(await UserWithViewAsync("#/menu-menu"));
            (await c.GetAsync("/api/DishCategory/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK, "Thực đơn tuần đọc tên nhóm món");
            (await c.GetAsync("/api/Dish/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK, "Thực đơn tuần đọc tên món");
            (await c.GetAsync("/api/DishCategory/GetAll?Page=1&PageSize=10")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "màn danh sách Nhóm món vẫn theo ma trận");
            (await c.PostAsync("/api/DishCategory/Insert", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "x" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await c.GetAsync("/api/PurchaseOrder/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "đơn mua không liên quan Thực đơn tuần");
        }
    }
}
