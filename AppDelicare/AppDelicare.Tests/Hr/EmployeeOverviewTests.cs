using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Hr
{
    /// <summary>UI-4 (D-68 g1, e): Employee/GetOverview – tài khoản, chỉ số tháng, ca 7 ngày, nghỉ phép, Cần chú ý; thông tin cá nhân chỉ khi có quyền Sửa adm-employee.</summary>
    public class EmployeeOverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory = factory;

        [Fact]
        public async Task Overview_profile_month_stats_alerts_and_personal_info_filtered_by_edit_right()
        {
            DateTime today = DateTime.UtcNow.AddHours(7).Date;
            Guid emp = Guid.NewGuid(), shift = Guid.NewGuid(), sLate = Guid.NewGuid(), sMissed = Guid.NewGuid(), user = Guid.NewGuid(), group = Guid.NewGuid();
            string code = "NV-UI4-" + emp.ToString("N")[..5];
            await _factory.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = emp, Code = code, FullName = "Nguyễn Văn UI4", Status = "Active", HireDate = today.AddMonths(-14), DateOfBirth = new DateTime(1990, 5, 1), Gender = "Nam", Address = "Số 1 Phố Bí Mật" });
                db.Shift.Add(new Shift { Id = shift, Code = "SANG", Name = "Ca sáng" });
                // Ca hôm nay đã chấm muộn 30′ + ra ca; ca hôm nay đã tới giờ mà chưa vào; ca ngày mai.
                db.ShiftAssignment.Add(new ShiftAssignment { Id = sLate, EmployeeId = emp, ShiftId = shift, WorkDate = today, PlannedStart = today, PlannedEnd = today.AddMinutes(1), AssignmentStatus = "Completed" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = sMissed, EmployeeId = emp, ShiftId = shift, WorkDate = today, PlannedStart = today, PlannedEnd = today.AddHours(23).AddMinutes(59), AssignmentStatus = "Confirmed" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = emp, ShiftId = shift, WorkDate = today.AddDays(1), PlannedStart = today.AddDays(1).AddHours(6), PlannedEnd = today.AddDays(1).AddHours(14), AssignmentStatus = "Planned" });
                db.AttendanceEvent.Add(new AttendanceEvent { Id = Guid.NewGuid(), EmployeeId = emp, ShiftAssignmentId = sLate, EventType = "CHECK_IN", EventAt = today.AddMinutes(30), IsValid = false, SourceChannel = "GPS" });
                db.AttendanceEvent.Add(new AttendanceEvent { Id = Guid.NewGuid(), EmployeeId = emp, ShiftAssignmentId = sLate, EventType = "CHECK_OUT", EventAt = today.AddMinutes(51), IsValid = false, SourceChannel = "GPS" });
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP-UI4-1", EmployeeId = emp, FromDate = today.AddDays(3), ToDate = today.AddDays(3), LeaveTypeCode = "ANNUAL", RequestStatus = "Approved", SubmittedAt = today });
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP-UI4-2", EmployeeId = emp, FromDate = today.AddDays(5), ToDate = today.AddDays(5), LeaveTypeCode = "ANNUAL", RequestStatus = "Pending", SubmittedAt = today });
                db.User.Add(new User { Id = user, UserName = code, IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "OPS-KIT-UI4" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/Employee/GetOverview?id={emp}")).Ok();
            o.GetProperty("canViewPersonal").GetBoolean().Should().BeTrue();
            o.GetProperty("employee").GetProperty("address").GetString().Should().Be("Số 1 Phố Bí Mật");
            o.GetProperty("tenureMonths").GetInt32().Should().Be(14);
            o.GetProperty("userName").GetString().Should().Be(code, "tài khoản web theo quy tắc tên đăng nhập = mã NV");
            o.GetProperty("listUserGroupName").EnumerateArray().Select(x => x.GetString()).Should().Contain("OPS-KIT-UI4");
            o.GetProperty("botLinked").GetBoolean().Should().BeFalse();
            o.GetProperty("monthShifts").GetInt32().Should().Be(2);
            o.GetProperty("monthLate").GetInt32().Should().Be(1);
            o.GetProperty("monthOnTimePct").GetDecimal().Should().Be(0);
            o.GetProperty("listUpcomingShift").GetArrayLength().Should().Be(3);
            o.GetProperty("listLeaveDate").EnumerateArray().Select(x => x.GetDateTime().Date).Should().Equal(today.AddDays(3));
            o.GetProperty("pendingRequestCount").GetInt32().Should().Be(1);
            o.GetProperty("listDayHours").GetArrayLength().Should().Be(30);
            o.GetProperty("listDayHours")[29].GetProperty("hours").GetDecimal().Should().Be(0.4m, "21 phút");
            o.GetProperty("listRecentAttendance").GetArrayLength().Should().Be(1);
            o.GetProperty("listAlert").EnumerateArray().Select(a => a.GetProperty("code").GetString())
                .Should().Equal("NOT_CHECKED_IN", "OUT_OF_RADIUS", "PENDING_REQUEST", "NO_BOT_LINK", "NO_PHONE");

            // Chỉ có quyền Xem adm-employee → không nhận ngày sinh / giới tính / địa chỉ (máy chủ lọc, D-68 e).
            using var viewer = _factory.CreateAuthorizedClient(await ViewerAsync());
            var v = (await viewer.GetResultAsync($"/api/Employee/GetOverview?id={emp}")).Ok();
            v.GetProperty("canViewPersonal").GetBoolean().Should().BeFalse();
            var e = v.GetProperty("employee");
            e.GetProperty("fullName").GetString().Should().Be("Nguyễn Văn UI4");
            e.GetProperty("address").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            e.GetProperty("dateOfBirth").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            e.GetProperty("gender").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            (await viewer.GetResultAsync($"/api/Employee/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");
        }

        /// <summary>User thường chỉ có quyền Xem màn adm-employee.</summary>
        private async Task<Guid> ViewerAsync()
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "vw-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "VW-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/adm-employee");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Nhân sự", MetaLink = "#/adm-employee", IsActive = true, LevelShow = 2 };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
            });
            return userId;
        }
    }
}
