using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// MENU-4 (D-75 d, e, f): bảng con ẩn khỏi menu → API kiểm quyền theo màn cha (4.3 a); dòng con của chứng từ không ghi được qua API chung (4.3 b);
    /// tiêu chí ATTP nhập trong Bộ kiểm tra (4.5); danh mục dùng chung có tên tiếng Việt (4.6).
    /// </summary>
    public class Menu4Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public Menu4Tests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>User thường, 1 nhóm, quyền trên các màn (link "#/screen") – mục menu bảng con (nếu có) đã ẩn IsActive = 0.</summary>
        private async Task<Guid> UserAsync(string[] view, string[]? edit = null)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "m4-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "M4-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (var (screen, canEdit) in view.Select(s => (s, false)).Concat((edit ?? []).Select(s => (s, true))))
                {
                    string link = "#/" + screen;
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = canEdit, AllowEdit = canEdit });
                }
                // Mục bảng con đã ẩn (script MENU4): có quyền ở đó cũng không còn tính.
                foreach (var child in new[] { "#/qc-checklist-item", "#/crm-order-line" })
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == child) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == child);
                    if (menu == null)
                    {
                        db.ObjectMenu.Add(new ObjectMenu { Id = Guid.NewGuid(), Name = child, MetaLink = child, IsActive = false, LevelShow = 2 });
                    }
                }
            });
            return userId;
        }

        private async Task<(Guid Checklist, Guid UsedItem, Guid FreeItem)> ChecklistAsync()
        {
            Guid ck = Guid.NewGuid(), used = Guid.NewGuid(), free = Guid.NewGuid();
            string code = "M4-" + ck.ToString("N")[..6];
            await _factory.SeedAsync(db =>
            {
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = ck, Code = code, Name = "Bộ kiểm tra " + code, Status = "Active" });
                db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = used, ChecklistId = ck, ItemCode = "TC-01", ItemName = "Nhiệt độ tủ mát", SortOrder = 1, ExpectedValue = "≤ 5", UnitCode = "°C" });
                db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = free, ChecklistId = ck, ItemCode = "TC-02", ItemName = "Vệ sinh bề mặt", SortOrder = 2 });
                db.FoodSafetyChecklistResult.Add(new FoodSafetyChecklistResult { Id = Guid.NewGuid(), ChecklistId = ck, ChecklistItemId = used, CheckDate = new DateTime(2026, 11, 20), CheckedAt = new DateTime(2026, 11, 20, 8, 0, 0), ResultCode = "PASS" });
                return Task.CompletedTask;
            });
            return (ck, used, free);
        }

        [Fact]
        public void Child_tables_map_to_existing_parent_screens()
        {
            var screens = ScreenMap.ByClass.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (cls, parent) in ScreenMap.ParentByClass)
            {
                ScreenMap.ByClass.Should().ContainKey(cls);
                screens.Should().Contain(parent.Screen, $"màn cha của {cls}");
            }
            foreach (var parent in ScreenMap.HiddenScreenParent.Values)
            {
                screens.Should().Contain(parent);
            }
            // Ghi dòng con = Sửa màn cha (Thêm dòng: Sửa hoặc Thêm).
            var insert = PermissionConvention.ChildPermission("crm-order", PermissionAction.Add);
            insert.Action.Should().Be(PermissionAction.Edit);
            insert.OrActions.Should().Equal(PermissionAction.Add);
        }

        [Fact]
        public async Task Child_api_uses_parent_screen_permission()
        {
            var (ck, _, _) = await ChecklistAsync();
            // Chỉ quyền Xem Bộ kiểm tra ATTP: đọc tiêu chí được, thêm thì 403.
            using var viewer = _factory.CreateAuthorizedClient(await UserAsync(["qc-checklist", "crm-order"]));
            (await viewer.GetAsync("/api/FoodSafetyChecklistItem/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await viewer.GetAsync("/api/OrderLine/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await viewer.PostAsync("/api/FoodSafetyChecklistItem/Insert", LegacyHttp.Form(("ChecklistId", ck), ("ItemName", "x")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            // Không có quyền màn cha → 403 (dù không còn mục menu riêng của bảng con).
            using var outsider = _factory.CreateAuthorizedClient(await UserAsync(["crm-customer"]));
            (await outsider.GetAsync("/api/FoodSafetyChecklistItem/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await outsider.GetAsync("/api/ContractMeal/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            // Quyền Sửa Bộ kiểm tra ATTP: thêm tiêu chí được – mã + thứ tự máy chủ tự cấp.
            using var editor = _factory.CreateAuthorizedClient(await UserAsync([], ["qc-checklist"]));
            (await editor.PostFormAsync("/api/FoodSafetyChecklistItem/Insert", ("ChecklistId", ck), ("ItemName", "Rửa tay trước khi vào bếp"))).Ok();
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Connection.DatabaseContext>();
            var added = await db.FoodSafetyChecklistItem.AsNoTracking().SingleAsync(x => x.ChecklistId == ck && x.ItemName == "Rửa tay trước khi vào bếp");
            added.ItemCode.Should().Be("TC-03");
            added.SortOrder.Should().Be(3);
        }

        [Fact]
        public async Task Used_checklist_item_cannot_change_content_or_be_deleted_but_can_be_reordered()
        {
            var (ck, used, free) = await ChecklistAsync();
            using var c = _factory.CreateAuthorizedClient();
            (await c.PutFormAsync("/api/FoodSafetyChecklistItem/Update", ("Id", used), ("ChecklistId", ck), ("ItemCode", "TC-01"), ("ItemName", "Nhiệt độ tủ đông"), ("SortOrder", 1)))
                .Fail("đã có kết quả kiểm tra");
            (await c.DeleteResultAsync($"/api/FoodSafetyChecklistItem/Delete?id={used}")).Fail("không xóa được");
            // Mô tả + thứ tự vẫn sửa được; tiêu chí chưa dùng thì sửa / xóa bình thường.
            (await c.PutFormAsync("/api/FoodSafetyChecklistItem/Update", ("Id", used), ("ChecklistId", ck), ("ItemCode", "TC-01"), ("ItemName", "Nhiệt độ tủ mát"), ("Description", "Đo đầu ca"), ("SortOrder", 1))).Ok();
            (await c.PutFormAsync("/api/FoodSafetyChecklistItem/Update", ("Id", free), ("ChecklistId", ck), ("ItemCode", "TC-02"), ("ItemName", "Vệ sinh bề mặt bàn"), ("SortOrder", 2))).Ok();
            (await c.PostFormAsync("/api/FoodSafetyChecklistItem/Reorder", ("ChecklistId", ck), ("ItemIds", used.ToString()))).Fail("đủ các tiêu chí");
            (await c.PostFormAsync("/api/FoodSafetyChecklistItem/Reorder", ("ChecklistId", ck), ("ItemIds", $"{free},{used}"))).Ok();
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Connection.DatabaseContext>();
            (await db.FoodSafetyChecklistItem.AsNoTracking().Where(x => x.ChecklistId == ck && !x.IsDeleted).OrderBy(x => x.SortOrder).Select(x => x.Id).ToListAsync())
                .Should().Equal(free, used);
            (await c.DeleteResultAsync($"/api/FoodSafetyChecklistItem/Delete?id={free}")).Ok();
        }

        /// <summary>4.3 b: dòng của đơn / PO / phiếu nhập / phiếu xuất / kiểm kê / QC không ghi được qua API chung – chỉ qua action nghiệp vụ của màn cha (có kiểm trạng thái).</summary>
        [Theory]
        [InlineData("OrderLine")]
        [InlineData("PurchaseOrderLine")]
        [InlineData("GoodsReceiptLine")]
        [InlineData("MaterialIssueLine")]
        [InlineData("StockCountLine")]
        [InlineData("QcInspectionLine")]
        public async Task Document_line_crud_is_blocked(string controller)
        {
            using var c = _factory.CreateAuthorizedClient();
            await Rejected(await c.PostAsync($"/api/{controller}/Insert", LegacyHttp.Form(("Quantity", 1m))));
            await Rejected(await c.PutAsync($"/api/{controller}/Update", LegacyHttp.Form(("Id", Guid.NewGuid()), ("Quantity", 1m))));
            await Rejected(await c.DeleteAsync($"/api/{controller}/Delete?id={Guid.NewGuid()}"));
        }

        /// <summary>Không ghi được: HTTP 400 (dữ liệu gửi lên thiếu) hoặc responseCode 400 (máy chủ chặn).</summary>
        private static async Task Rejected(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                return;
            }
            (await LegacyResult.ReadAsync(response)).Code.Should().Be(400);
        }

        [Fact]
        public void Reference_lists_have_vietnamese_titles()
        {
            ReferenceCatalogs.TitleOf("MealType").Should().Be("Loại suất (bữa ăn)");
            ReferenceCatalogs.TitleOf("ChuaCo").Should().Be("ChuaCo");
            foreach (var list in ReferenceCatalogs.Columns.Values.Select(v => v.List).Distinct())
            {
                ReferenceCatalogs.ListTitles.Should().ContainKey(list);
            }
        }
    }
}
