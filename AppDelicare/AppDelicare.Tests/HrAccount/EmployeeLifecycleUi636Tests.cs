using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.HrAccount
{
    /// <summary>
    /// UI-6.36 (D-117): luật nhân viên + cho nghỉ việc đúng quy trình.
    /// - Tạo: họ tên bắt buộc; SĐT sai định dạng / trùng người đang làm → lỗi (trùng người đã nghỉ thì cho); công ty theo đơn vị; Status gửi lên bị bỏ → Đang làm việc.
    /// - Sửa: Status gửi lên bị bỏ qua.
    /// - Nghỉ việc hôm nay: Đã nghỉ việc, khóa tài khoản, hủy ca sau ngày nghỉ, lịch sử có lý do. Hẹn ngày tương lai: vẫn đang làm, job đúng ngày khóa, chạy lại không nhân đôi.
    /// - Tạm nghỉ → quay lại: yêu cầu mở lại tài khoản. Hàng loạt: bỏ qua người đã có / đang chờ tài khoản.
    /// </summary>
    [Collection("AccountLockResignedJob")]
    public class EmployeeLifecycleUi636Tests(InMemoryApiFactory factory) : HrAccountTestBase(factory), IClassFixture<InMemoryApiFactory>
    {
        private async Task<string> PostJsonAsync(HttpClient c, string url, object body) => await (await c.PostAsJsonAsync(url, body)).Content.ReadAsStringAsync();

        [Fact]
        public async Task Insert_and_update_apply_name_phone_company_and_status_rules()
        {
            Guid company = Guid.NewGuid(), unit = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = unit, CompanyId = company, Code = "U636", Name = "Bếp U636", UnitType = "BRANCH" });
                return Task.CompletedTask;
            });
            await EmployeeAsync("U636-OLD", "0912636001");
            await EmployeeAsync("U636-LEFT", "0912636002", status: EmployeeStatus.Resigned);
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/Employee/Insert", ("FullName", ""), ("Phone", "0912636009"))).Fail("họ tên");
            (await c.PostFormAsync("/api/Employee/Insert", ("FullName", "[DEMO] U636 A"), ("Phone", "12345"))).Fail("định dạng");
            (await c.PostFormAsync("/api/Employee/Insert", ("FullName", "[DEMO] U636 A"), ("Phone", "0912 636 001"))).Fail("NV U636-OLD");
            (await c.PostFormAsync("/api/Employee/Insert", ("FullName", "[DEMO] U636 B"), ("Phone", "0912636002"), ("Status", EmployeeStatus.Resigned),
                ("OrganizationUnitId", unit), ("CompanyId", Guid.NewGuid()))).Ok();
            var b = await _factory.QueryAsync(db => db.Employee.AsNoTracking().SingleAsync(e => e.FullName == "[DEMO] U636 B"));
            b.Status.Should().Be(EmployeeStatus.Active, "trạng thái khi tạo luôn Đang làm việc");
            b.CompanyId.Should().Be(company, "công ty lấy theo đơn vị");
            b.HireDate.Should().Be(Today, "ngày vào làm trống → hôm nay");

            (await c.PutFormAsync("/api/Employee/Update", ("Id", b.Id), ("FullName", "[DEMO] U636 B2"), ("Phone", "0912636002"), ("Status", EmployeeStatus.Resigned), ("OrganizationUnitId", unit))).Ok();
            var b2 = await _factory.QueryAsync(db => db.Employee.AsNoTracking().SingleAsync(e => e.Id == b.Id));
            b2.FullName.Should().Be("[DEMO] U636 B2");
            b2.Status.Should().Be(EmployeeStatus.Active, "đổi trạng thái chỉ qua Cho nghỉ việc / Tạm nghỉ");

            (await c.GetResultAsync("/api/Employee/CheckPhone?phone=0912636001")).Ok().GetProperty("duplicate").GetBoolean().Should().BeTrue();
            (await c.GetResultAsync("/api/Employee/CheckPhone?phone=0912636001&exceptId=" + b.Id)).Ok().GetProperty("valid").GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task Resign_today_locks_account_cancels_later_shifts_and_writes_reason()
        {
            Guid person = await PersonAsync("Người U636R");
            Guid emp = await EmployeeAsync("U636-R", null, person);
            Guid user = await UserAsync("u636.r", personId: person);
            Guid shift = Guid.NewGuid(), kept = Guid.NewGuid(), later = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "U636-TRUA", Name = "Trưa U636", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = kept, ShiftId = shift, EmployeeId = emp, WorkDate = Today, AssignmentStatus = "Planned" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = later, ShiftId = shift, EmployeeId = emp, WorkDate = Today.AddDays(2), AssignmentStatus = "Planned" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var preview = (await c.GetResultAsync($"/api/Employee/PreviewResign?id={emp}&resignDate={Today:yyyy-MM-dd}")).Ok();
            preview.GetProperty("listShiftDate").GetArrayLength().Should().Be(1);
            preview.GetProperty("hasActiveAccount").GetBoolean().Should().BeTrue();

            (await PostJsonAsync(c, "/api/Employee/Resign", new { id = emp, resignDate = Today })).Should().Contain("lý do");
            (await PostJsonAsync(c, "/api/Employee/Resign", new { id = emp, resignDate = Today, reasonCode = "QUIT", note = "Chuyển nơi khác" })).Should().Contain("\"responseCode\":200");

            (await _factory.QueryAsync(db => db.Employee.AsNoTracking().Where(e => e.Id == emp).Select(e => e.Status).SingleAsync())).Should().Be(EmployeeStatus.Resigned);
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == user).Select(u => u.IsActive).SingleAsync())).Should().BeFalse();
            (await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.Id == later).Select(a => a.IsDeleted).SingleAsync())).Should().BeTrue("ca sau ngày nghỉ bị hủy");
            (await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.Id == kept).Select(a => a.IsDeleted).SingleAsync())).Should().BeFalse("ca ngày nghỉ giữ");
            (await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.IdChange == emp && a.Action == "Resign").Select(a => a.Changes).SingleAsync())).Should().Contain("QUIT").And.Contain("Chuyển nơi khác");
            (await PostJsonAsync(c, "/api/Employee/Resign", new { id = emp, resignDate = Today, reasonCode = "QUIT" })).Should().Contain("đã nghỉ việc");
        }

        [Fact]
        public async Task Resign_in_future_keeps_working_until_the_day_and_job_is_idempotent()
        {
            Guid person = await PersonAsync("Người U636F");
            Guid emp = await EmployeeAsync("U636-F", null, person);
            Guid user = await UserAsync("u636.f", personId: person);
            using var c = _factory.CreateAuthorizedClient();
            (await PostJsonAsync(c, "/api/Employee/Resign", new { id = emp, resignDate = Today.AddDays(3), reasonCode = "CONTRACT_END" })).Should().Contain("\"responseCode\":200");
            (await _factory.QueryAsync(db => db.Employee.AsNoTracking().Where(e => e.Id == emp).Select(e => e.Status).SingleAsync())).Should().Be(EmployeeStatus.Active);
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == user).Select(u => u.IsActive).SingleAsync())).Should().BeTrue("chưa tới ngày nghỉ");
            var list = (await c.GetResultAsync("/api/Employee/GetList?keyword=U636-F")).Ok().GetProperty("listItem");
            list[0].GetProperty("scheduledResignDate").GetDateTime().Should().Be(Today.AddDays(3));

            (await c.PostFormAsync("/api/Job/Run", ("jobCode", "AccountLockResigned"))).Ok();
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == user).Select(u => u.IsActive).SingleAsync())).Should().BeTrue("job chạy trước ngày nghỉ không khóa");

            _factory.Clock.FixedNow = Today.AddDays(3).AddHours(1);
            try
            {
                (await c.PostFormAsync("/api/Job/Run", ("jobCode", "AccountLockResigned"))).Ok();
                (await c.PostFormAsync("/api/Job/Run", ("jobCode", "AccountLockResigned"))).Ok();
                (await _factory.QueryAsync(db => db.Employee.AsNoTracking().Where(e => e.Id == emp).Select(e => e.Status).SingleAsync())).Should().Be(EmployeeStatus.Resigned);
                (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == user).Select(u => u.IsActive).SingleAsync())).Should().BeFalse();
                (await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().CountAsync(a => a.IdChange == emp && a.Action == "Resign"))).Should().Be(1, "chạy lại không nhân đôi");
            }
            finally
            {
                _factory.Clock.FixedNow = Today.AddHours(9);
            }
        }

        [Fact]
        public async Task Pause_then_return_requests_reopen_and_bulk_account_skips_existing()
        {
            Guid person = await PersonAsync("Người U636S");
            Guid emp = await EmployeeAsync("U636-S", null, person);
            Guid user = await UserAsync("u636.s", personId: person);
            await GroupsAsync("FIN-IT");
            using var c = _factory.CreateAuthorizedClient();
            (await PostJsonAsync(c, "/api/Employee/SetStatus", new { id = emp, status = "Resigned" })).Should().Contain("Tạm nghỉ hoặc Quay lại");
            (await PostJsonAsync(c, "/api/Employee/SetStatus", new { id = emp, status = EmployeeStatus.Inactive, note = "Nghỉ thai sản" })).Should().Contain("\"responseCode\":200");
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == user).Select(u => u.IsActive).SingleAsync())).Should().BeFalse("tạm nghỉ khóa tài khoản");
            (await PostJsonAsync(c, "/api/Employee/SetStatus", new { id = emp, status = EmployeeStatus.Active })).Should().Contain("\"responseCode\":200");
            (await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().AnyAsync(a => a.BusinessId == emp && a.BusinessType == AccountLifecycleCodes.ReopenBusinessType))).Should().BeTrue("quay lại → yêu cầu mở lại");

            Guid fresh = await EmployeeAsync("U636-N1");
            var bulk = await PostJsonAsync(c, "/api/Employee/RequestAccountMany", new { listId = new[] { emp, fresh } });
            bulk.Should().Contain("\"done\":1").And.Contain("\"skipped\":1");
            (await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().CountAsync(a => a.BusinessId == fresh && a.BusinessType == AccountLifecycleCodes.CreateBusinessType))).Should().Be(1);
        }
    }
}
