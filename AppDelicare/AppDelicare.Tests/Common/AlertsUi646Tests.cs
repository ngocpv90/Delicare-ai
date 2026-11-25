using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// UI-6.46 (D-125 c): chuông có cảnh báo mới của đợt UI-6 – tài khoản của người đã nghỉ việc còn mở (→ Tài khoản), ca thiếu người 7 ngày tới (→ Phân ca);
    /// route trỏ màn có thật; đơn đổi ca mở trong "Nghỉ &amp; đổi ca" (không còn link màn ẩn hr-swap).
    /// </summary>
    public class AlertsUi646Tests
    {
        private static readonly DateTime Now = new(2032, 6, 7, 9, 0, 0);

        [Fact]
        public async Task Bell_shows_resigned_open_accounts_and_short_shifts()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            f.Settings["BusinessRules:Hr:ShiftMinStaff:T646:S646"] = "2";
            Guid unit = Guid.NewGuid(), shift = Guid.NewGuid(), e1 = Guid.NewGuid(), e2 = Guid.NewGuid(), person = Guid.NewGuid();
            DateTime d1 = Now.Date.AddDays(1);
            await f.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = unit, Code = "T646", Name = "Tổ 646" });
                db.Shift.Add(new Shift { Id = shift, Code = "S646", Name = "Sáng 646", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                db.Employee.Add(new Employee { Id = e1, Code = "nv646-1", EmployeeCode = "nv646-1", FullName = "NV 646 1", OrganizationUnitId = unit, Status = "Active" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = e1, ShiftId = shift, WorkDate = d1, PlannedStart = d1.AddHours(6), PlannedEnd = d1.AddHours(14), AssignmentStatus = "Planned" });
                // Người đã nghỉ việc mà tài khoản còn mở.
                db.Person.Add(new Person { Id = person, FullName = "Người 646" });
                db.Employee.Add(new Employee { Id = e2, Code = "nv646-2", EmployeeCode = "nv646-2", FullName = "Người 646", PersonId = person, Status = EmployeeStatus.Resigned });
                db.User.Add(new User { Id = Guid.NewGuid(), UserName = "u646.left", IsActive = true, PersonId = person, AccountType = "INTERNAL" });
                return Task.CompletedTask;
            });
            using var admin = f.CreateAuthorizedClient();
            var res = await admin.GetResultAsync("/api/Common/GetAlerts");
            res.Code.Should().Be(200, res.Message);
            var alerts = res.ListData.EnumerateArray().ToDictionary(a => a.GetProperty("code").GetString()!);
            alerts.Should().ContainKey(CommonService.AlertCode.AccountResignedOpen);
            alerts[CommonService.AlertCode.AccountResignedOpen].GetProperty("route").GetString().Should().Be("#/adm-user");
            alerts[CommonService.AlertCode.AccountResignedOpen].GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(1);
            alerts.Should().ContainKey(CommonService.AlertCode.ShiftShort);
            alerts[CommonService.AlertCode.ShiftShort].GetProperty("route").GetString().Should().Be("#/hr-shift");
        }

        [Fact]
        public void Swap_approval_opens_leave_and_swap_screen()
        {
            Services.Implement.Approval.ApprovalSummaries.TypeOf(ShiftSwapStatus.BusinessType).Screen.Should().Be("hr-leave");
        }

        [Fact]
        public async Task Merged_leave_and_swap_approval_right_approves_swaps_old_and_new()
        {
            using var f = new InMemoryApiFactory();
            using var scope = f.Services.CreateScope();
            var ps = scope.ServiceProvider.GetRequiredService<Services.Interface.IPermissionService>();
            var menus = new Dictionary<string, Services.Interface.MenuRights> { ["#/wf-inbox?type=hr-leave"] = new(true, false, true, false, false, false, false), ["#/hr-leave"] = new(true, true, true, false, false, false, false) };
            var p = new Services.Interface.UserPermissions(false, new HashSet<string>(), menus);
            // Quy trình cũ đã lưu mục "hr-swap" (ẩn từ UI-6.39) → vẫn duyệt được bằng mục Nghỉ & đổi ca.
            ps.IsAllowed(p, "hr-swap", Services.Interface.PermissionAction.Approve).Should().BeTrue();
            ps.IsAllowed(p, ShiftSwapStatus.ScreenId, Services.Interface.PermissionAction.Approve).Should().BeTrue();
            ps.IsAllowed(p, HrScreen.Swap, Services.Interface.PermissionAction.Add).Should().BeTrue("lập đơn đổi ca hộ theo quyền Nghỉ & đổi ca");
            // Người duyệt nhận tin: tìm theo mục còn hiện.
            await f.SeedAsync(db =>
            {
                Guid g = Guid.NewGuid(), u = Guid.NewGuid(), m = Guid.NewGuid();
                db.ObjectMenu.Add(new ObjectMenu { Id = m, Name = "Nghỉ & đổi ca", MetaLink = "#/wf-inbox?type=hr-leave", IsActive = true, LevelShow = 2 });
                db.UserGroup.Add(new UserGroup { Id = g, Name = "G646", Code = "G646" });
                db.User.Add(new User { Id = u, UserName = "u646.appr", IsActive = true });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m, AllowAccess = true, AllowEdit = true });
                return Task.CompletedTask;
            });
            var ids = await f.QueryAsync(db => Services.Implement.Messaging.RecipientResolver.UsersWithApproveAsync(db, "hr-swap"));
            ids.Should().NotBeEmpty();
        }
    }
}
