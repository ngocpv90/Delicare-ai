using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.HrAccount
{
    /// <summary>
    /// UI-6.43 (D-124): nhóm quyền có Mã – quyền / người nhận / gợi ý nhóm tìm theo mã (tên tiếng Việt đổi được); mã G2, không trùng, khóa khi đang là bước duyệt;
    /// khối theo danh mục; mức quyền (Không / Xem / Xem, in &amp; xuất / Xem &amp; sửa / Toàn quyền / Tùy chỉnh); SaveMany 1 lần + AuditChange + bật nhóm menu cha;
    /// xem trước nháp không ghi; so sánh 2 nhóm; ngừng dùng nhóm bị chặn khi còn tài khoản / đang duyệt; bỏ tài khoản không để tài khoản hết nhóm.
    /// </summary>
    public class UserGroupUi643Tests(InMemoryApiFactory factory) : HrAccountTestBase(factory), IClassFixture<InMemoryApiFactory>
    {
        private static async Task<string> PostJson(HttpClient c, string url, object body) => await (await c.PostAsJsonAsync(url, body)).Content.ReadAsStringAsync();

        [Fact]
        public async Task Permission_groups_and_recipients_match_by_code_after_vietnamese_rename()
        {
            Guid user = await UserAsync("u643.wh");
            Guid gid = await _factory.QueryAsync(db => db.UserClaim.Where(c => c.UserId == user).Select(c => c.UserGroupId!.Value).SingleAsync());
            await _factory.SeedAsync(async db => { var g = await db.UserGroup.SingleAsync(x => x.Id == gid); g.Code = "T643-WH"; g.Name = "Thủ kho T643"; });
            using var scope = _factory.Services.CreateScope();
            var perm = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            perm.Invalidate(user);
            var p = await perm.GetAsync(user);
            p.Groups.Should().Contain("T643-WH").And.Contain("Thủ kho T643");
        }

        [Fact]
        public async Task Code_is_normalized_unique_block_checked_and_locked_when_used_by_approval_step()
        {
            using var admin = _factory.CreateAuthorizedClient();
            (await admin.PostFormAsync("/api/UserGroup/Insert", ("Name", ""), ("Code", "X"))).Fail("tên nhóm");
            (await admin.PostFormAsync("/api/UserGroup/Insert", ("Name", "Nhóm T643 A"), ("Code", "t643-a"), ("GroupBlock", "OPS"), ("Description", "Thử"))).Ok();
            var a = await _factory.QueryAsync(db => db.UserGroup.AsNoTracking().SingleAsync(g => g.Name == "Nhóm T643 A"));
            a.Code.Should().Be("T643-A");
            a.GroupBlock.Should().Be("OPS");
            (await admin.PostFormAsync("/api/UserGroup/Insert", ("Name", "Nhóm T643 B"), ("Code", "T643-A"))).Fail("đã có");
            (await admin.PostFormAsync("/api/UserGroup/Insert", ("Name", "Nhóm T643 C"), ("Code", "T643-C"), ("GroupBlock", "ABC"))).Fail("Khối");
            // Đổi tên (giữ mã) được; đang là bước duyệt → không đổi mã.
            await _factory.SeedAsync(db => { db.ApprovalStep.Add(new ApprovalStep { Id = Guid.NewGuid(), ApprovalRequestId = Guid.NewGuid(), StepNo = 1, ApproverRoleCode = "T643-A" }); return Task.CompletedTask; });
            (await admin.PutFormAsync("/api/UserGroup/Update", ("Id", a.Id), ("Name", "Nhóm T643 A mới"))).Ok();
            (await _factory.QueryAsync(db => db.UserGroup.AsNoTracking().Where(g => g.Id == a.Id).Select(g => g.Code).SingleAsync())).Should().Be("T643-A");
            (await admin.PutFormAsync("/api/UserGroup/Update", ("Id", a.Id), ("Name", "Nhóm T643 A mới"), ("Code", "T643-Z"))).Fail("không đổi được mã");
            // Ngừng dùng: đang là người duyệt → chặn.
            (await PostJson(admin, "/api/UserGroup/Retire", new { id = a.Id })).Should().Contain("Không ngừng dùng được");
        }

        [Fact]
        public async Task Levels_save_many_audits_and_preview_draft_does_not_write_and_compare_lists_differences()
        {
            Guid parent = Guid.NewGuid(), s1 = Guid.NewGuid(), s2 = Guid.NewGuid(), g1 = Guid.NewGuid(), g2 = Guid.NewGuid();
            Guid member = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ObjectMenu.Add(new ObjectMenu { Id = parent, Name = "Nhóm T643", MetaLink = "#/group/t643", IsActive = true, LevelShow = 1 });
                db.ObjectMenu.Add(new ObjectMenu { Id = s1, Name = "Màn T643 1", MetaLink = "#/t643-1", IsActive = true, LevelShow = 2, ParentId = parent });
                db.ObjectMenu.Add(new ObjectMenu { Id = s2, Name = "Màn T643 2", MetaLink = "#/t643-2", IsActive = true, LevelShow = 2, ParentId = parent });
                db.UserGroup.Add(new UserGroup { Id = g1, Name = "Nhóm L643 1", Code = "L643-1" });
                db.UserGroup.Add(new UserGroup { Id = g2, Name = "Nhóm L643 2", Code = "L643-2" });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g2, ObjectMenuId = s1, AllowAccess = true });
                db.User.Add(new User { Id = member, UserName = "u643.member", IsActive = true });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = member, UserGroupId = g1 });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var draft = new { userGroupId = g1, items = new object[] { new { objectMenuId = s1, level = "S" }, new { objectMenuId = s2, level = "IX" } } };
            var prev = await PostJson(admin, "/api/UserRole/GetPreviewDraft", draft);
            prev.Should().Contain("Màn T643 1").And.Contain("Nhóm T643").And.Contain("\"screenCount\":2");
            (await _factory.QueryAsync(db => db.UserRole.CountAsync(r => r.UserGroupId == g1))).Should().Be(0, "xem trước không ghi");

            (await PostJson(admin, "/api/UserRole/SaveMany", draft)).Should().Contain("\"data\":2");
            var levels = (await admin.GetResultAsync("/api/UserRole/GetLevels?userGroupId=" + g1)).Ok().GetProperty("listItem").EnumerateArray().ToList();
            levels.Single(l => l.GetProperty("objectMenuId").GetGuid() == s1).GetProperty("level").GetString().Should().Be("S");
            levels.Single(l => l.GetProperty("objectMenuId").GetGuid() == s2).GetProperty("level").GetString().Should().Be("IX");
            (await _factory.QueryAsync(db => db.UserRole.AnyAsync(r => r.UserGroupId == g1 && r.ObjectMenuId == parent && r.AllowAccess))).Should().BeTrue("nhóm menu cha được bật Xem");
            var audit = await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.IdChange == g1 && a.Action == "RoleSaveMany").Select(a => a.Changes).SingleAsync());
            audit.Should().Contain("Màn T643 1: Không => Xem & sửa");
            // Lưu lại cùng nội dung → 0 thay đổi, không thêm dòng lịch sử.
            (await PostJson(admin, "/api/UserRole/SaveMany", draft)).Should().Contain("\"data\":0");
            // Cờ lẻ → Tùy chỉnh.
            (await PostJson(admin, "/api/UserRole/SaveMany", new { userGroupId = g1, items = new object[] { new { objectMenuId = s2, allowAccess = true, allowDelete = true } } })).Should().Contain("\"data\":1");
            (await admin.GetResultAsync("/api/UserRole/GetLevels?userGroupId=" + g1)).Ok().GetProperty("listItem").EnumerateArray()
                .Single(l => l.GetProperty("objectMenuId").GetGuid() == s2).GetProperty("level").GetString().Should().Be("CUSTOM");

            // Mục "Phê duyệt › loại": D = Truy cập + Sửa (D-31 – được duyệt).
            (await PostJson(admin, "/api/UserRole/SaveMany", new { userGroupId = g1, items = new object[] { new { objectMenuId = s1, level = "D" } } })).Should().Contain("\"data\":1");
            var d1 = await _factory.QueryAsync(db => db.UserRole.AsNoTracking().SingleAsync(r => r.UserGroupId == g1 && r.ObjectMenuId == s1));
            (d1.AllowAccess && d1.AllowEdit && !d1.AllowAdd && !d1.AllowPrint).Should().BeTrue();
            (await PostJson(admin, "/api/UserRole/SaveMany", new { userGroupId = g1, items = new object[] { new { objectMenuId = s1, level = "S" } } })).Should().Contain("\"data\":1");

            var cmp = (await admin.GetResultAsync($"/api/UserRole/Compare?groupA={g1}&groupB={g2}")).Ok();
            var rows = cmp.GetProperty("listItem").EnumerateArray().Where(x => x.GetProperty("name").GetString()!.StartsWith("Màn T643")).ToList();
            rows.Should().HaveCount(2);
            rows.Single(x => x.GetProperty("objectMenuId").GetGuid() == s1).GetProperty("levelB").GetString().Should().Be("X");

            // Bỏ quyền màn con cuối cùng → nhóm menu cha tắt theo.
            (await PostJson(admin, "/api/UserRole/SaveMany", new { userGroupId = g1, items = new object[] { new { objectMenuId = s1, level = "NONE" }, new { objectMenuId = s2, level = "NONE" } } })).Should().Contain("\"data\":2");
            (await _factory.QueryAsync(db => db.UserRole.AnyAsync(r => r.UserGroupId == g1 && r.ObjectMenuId == parent && r.AllowAccess))).Should().BeFalse("nhóm menu không còn màn nào");

            // Ngừng dùng: còn tài khoản → chặn; bỏ tài khoản cuối khỏi nhóm duy nhất → bỏ qua.
            (await PostJson(admin, "/api/UserGroup/Retire", new { id = g1 })).Should().Contain("còn 1 tài khoản");
            (await PostJson(admin, "/api/UserGroup/RemoveUsers", new { userGroupId = g1, userIds = new[] { member } })).Should().Contain("\"done\":0").And.Contain("u643.member");
            (await PostJson(admin, "/api/UserGroup/AddUsers", new { userGroupId = g2, userIds = new[] { member } })).Should().Contain("\"done\":1");
            (await PostJson(admin, "/api/UserGroup/RemoveUsers", new { userGroupId = g1, userIds = new[] { member } })).Should().Contain("\"done\":1");
            (await PostJson(admin, "/api/UserGroup/Retire", new { id = g1 })).Should().Contain("\"responseCode\":200");
            (await _factory.QueryAsync(db => db.UserGroup.AsNoTracking().Where(g => g.Id == g1).Select(g => g.IsDeleted).SingleAsync())).Should().BeTrue();
        }

        [Fact]
        public async Task List_groups_by_block_with_counts()
        {
            Guid g = Guid.NewGuid(), u = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UserGroup.Add(new UserGroup { Id = g, Name = "Nhóm D643", Code = "D643", GroupBlock = "QA" });
                db.User.Add(new User { Id = u, UserName = "u643.list", IsActive = true });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var list = (await admin.GetResultAsync("/api/UserGroup/GetList?keyword=D643")).Ok();
            var row = list.GetProperty("listItem")[0];
            row.GetProperty("groupBlockName").GetString().Should().Be("QA / ATTP");
            row.GetProperty("accounts").GetInt32().Should().Be(1);
            list.GetProperty("groups").GetInt32().Should().BeGreaterThan(0);
        }
    }
}
