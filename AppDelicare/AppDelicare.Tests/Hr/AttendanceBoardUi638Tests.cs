using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Hr
{
    /// <summary>
    /// UI-6.38 (D-119): Bổ sung giờ (thiếu ra ca → Hoàn thành, giờ công đúng; ngoài khung / quá 3 ngày / đã có lần ra → lỗi; ca Vắng bổ sung vào + ra → Hoàn thành),
    /// xác nhận ngoài bán kính (thiếu ghi chú → lỗi; sau xác nhận bảng công không còn "!"), bảng công tháng khớp GetTimesheet, tự chuyển Vắng chạy 2 lần không đổi thêm.
    /// </summary>
    public class AttendanceBoardUi638Tests
    {
        private static readonly DateTime Now = new(2025, 11, 26, 23, 40, 0);

        private sealed record World(Guid Emp, Guid Shift, Guid Done, Guid MissingOut, Guid Absent, Guid Old, Guid OutsideEvent, Guid Today);

        private static async Task<World> SeedAsync(InMemoryApiFactory f)
        {
            Guid emp = Guid.NewGuid(), shift = Guid.NewGuid();
            DateTime d0 = Now.Date;
            var w = new World(emp, shift, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            await f.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = emp, Code = "NV638", FullName = "NV 638", Status = "Active" });
                db.Shift.Add(new Shift { Id = shift, Code = "C638", Name = "Chiều 638", StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(21, 0, 0), Status = "Active" });
                ShiftAssignment A(Guid id, DateTime d, string st) => new() { Id = id, EmployeeId = emp, ShiftId = shift, WorkDate = d, PlannedStart = d.AddHours(13), PlannedEnd = d.AddHours(21), AssignmentStatus = st };
                db.ShiftAssignment.AddRange(A(w.Done, d0.AddDays(-3), "Completed"), A(w.MissingOut, d0.AddDays(-1), "CheckedIn"), A(w.Absent, d0.AddDays(-2), "Absent"),
                    A(w.Old, d0.AddDays(-5), "CheckedIn"), A(w.Today, d0, "Planned"));
                AttendanceEvent E(Guid a, DateTime at, string type, bool valid = true, Guid? id = null) => new() { Id = id ?? Guid.NewGuid(), EmployeeId = emp, ShiftAssignmentId = a, EventType = type, EventAt = at, IsValid = valid, SourceChannel = "GPS", DistanceMeters = valid ? 10 : 420 };
                db.AttendanceEvent.AddRange(E(w.Done, d0.AddDays(-3).AddHours(13), "CHECK_IN", false, w.OutsideEvent), E(w.Done, d0.AddDays(-3).AddHours(21), "CHECK_OUT"),
                    E(w.MissingOut, d0.AddDays(-1).AddHours(12.9), "CHECK_IN"), E(w.Old, d0.AddDays(-5).AddHours(13), "CHECK_IN"));
                return Task.CompletedTask;
            });
            return w;
        }

        private static async Task<string> Post(HttpClient c, string url, object body) => await (await c.PostAsJsonAsync(url, body)).Content.ReadAsStringAsync();

        [Fact]
        public async Task Supplement_completes_shift_and_rejects_out_of_window_old_and_duplicate()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            var w = await SeedAsync(f);
            using var c = f.CreateAuthorizedClient();
            DateTime y = Now.Date.AddDays(-1);
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.MissingOut, eventType = "CHECK_OUT", eventAt = y.AddHours(21), reasonCode = "" })).Should().Contain("lý do");
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.MissingOut, eventType = "CHECK_OUT", eventAt = y.AddHours(24).AddMinutes(30), reasonCode = "FORGOT" })).Should().Contain("khung ca");
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.Old, eventType = "CHECK_OUT", eventAt = Now.Date.AddDays(-5).AddHours(21), reasonCode = "FORGOT" })).Should().Contain("3 ngày");
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.MissingOut, eventType = "CHECK_OUT", eventAt = y.AddHours(22).AddMinutes(5), reasonCode = "FORGOT" })).Should().Contain("\"responseCode\":200");
            (await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.Id == w.MissingOut).Select(a => a.AssignmentStatus).SingleAsync())).Should().Be(ShiftAssignmentStatus.Completed);
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.MissingOut, eventType = "CHECK_OUT", eventAt = y.AddHours(22), reasonCode = "FORGOT" })).Should().Contain("đã có lần");
            var detail = (await c.GetResultAsync($"/api/AttendanceEvent/GetTimesheetDetail?FromDate={y:yyyy-MM-dd}&ToDate={y:yyyy-MM-dd}&EmployeeId={w.Emp}&Page=1&PageSize=10")).ListData[0];
            detail.GetProperty("workedMinutes").GetInt32().Should().Be(551, "12:54 → 22:05");
            (await f.QueryAsync(db => db.AttendanceEvent.AsNoTracking().Where(e => e.ShiftAssignmentId == w.MissingOut && e.EventType == "CHECK_OUT").Select(e => e.Note).SingleAsync())).Should().StartWith("[BỔ SUNG] Quên chấm công");

            // Ca Vắng: bổ sung vào + ra → Hoàn thành.
            DateTime d2 = Now.Date.AddDays(-2);
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.Absent, eventType = "CHECK_IN", eventAt = d2.AddHours(13), reasonCode = "PHONE" })).Should().Contain("\"responseCode\":200");
            (await Post(c, "/api/AttendanceEvent/Supplement", new { employeeId = w.Emp, shiftAssignmentId = w.Absent, eventType = "CHECK_OUT", eventAt = d2.AddHours(21), reasonCode = "PHONE" })).Should().Contain("\"responseCode\":200");
            (await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.Id == w.Absent).Select(a => a.AssignmentStatus).SingleAsync())).Should().Be(ShiftAssignmentStatus.Completed);
        }

        [Fact]
        public async Task Confirm_valid_needs_note_and_month_grid_matches_timesheet()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            var w = await SeedAsync(f);
            using var c = f.CreateAuthorizedClient();
            var grid = (await c.GetResultAsync("/api/AttendanceEvent/GetMonthGrid?month=2025-11")).Ok();
            var row = grid.GetProperty("listRow").EnumerateArray().First(r => r.GetProperty("employeeId").GetGuid() == w.Emp);
            row.GetProperty("cells").GetProperty(Now.Date.AddDays(-3).ToString("yyyy-MM-dd")).GetProperty("symbol").GetString().Should().Be("!");
            row.GetProperty("cells").GetProperty(Now.Date.AddDays(-2).ToString("yyyy-MM-dd")).GetProperty("symbol").GetString().Should().Be("V");
            row.GetProperty("cells").GetProperty(Now.Date.AddDays(-1).ToString("yyyy-MM-dd")).GetProperty("symbol").GetString().Should().Be("T");
            grid.GetProperty("outsideUnconfirmed").GetInt32().Should().Be(1);

            var ts = (await c.GetResultAsync($"/api/AttendanceEvent/GetTimesheet?FromDate=2025-11-01&ToDate=2025-11-30&EmployeeId={w.Emp}&Page=1&PageSize=10")).ListData[0];
            row.GetProperty("absent").GetInt32().Should().Be(ts.GetProperty("absent").GetInt32());
            row.GetProperty("hours").GetDecimal().Should().Be(Math.Round(ts.GetProperty("workedMinutes").GetInt32() / 60m, 2));
            row.GetProperty("work").GetInt32().Should().Be(ts.GetProperty("present").GetInt32());

            (await Post(c, "/api/AttendanceEvent/ConfirmValid", new { id = w.OutsideEvent, note = "" })).Should().Contain("lý do");
            (await Post(c, "/api/AttendanceEvent/ConfirmValid", new { id = w.OutsideEvent, note = "Nhận xe ở bãi" })).Should().Contain("\"responseCode\":200");
            var grid2 = (await c.GetResultAsync("/api/AttendanceEvent/GetMonthGrid?month=2025-11")).Ok();
            grid2.GetProperty("outsideUnconfirmed").GetInt32().Should().Be(0);
            (await c.GetResultAsync($"/api/AttendanceEvent/GetTimesheet?FromDate=2025-11-01&ToDate=2025-11-30&EmployeeId={w.Emp}&Page=1&PageSize=10")).ListData[0].GetProperty("invalidEvents").GetInt32().Should().Be(0);

            var day = (await c.GetResultAsync("/api/AttendanceEvent/GetDayBoard")).Ok();
            day.GetProperty("listNeedAction").EnumerateArray().Select(x => x.GetProperty("kind").GetString()).Should().Contain("MISSING_OUT");
            var xlsx = await c.GetAsync("/api/AttendanceEvent/ExportTimesheet?month=2025-11");
            xlsx.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheet");
        }

        [Fact]
        public async Task Auto_mark_absent_job_is_idempotent()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            var w = await SeedAsync(f);
            using var c = f.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Job/Run", ("jobCode", "MarkAbsent"))).Ok();
            (await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.Id == w.Today).Select(a => a.AssignmentStatus).SingleAsync())).Should().Be(ShiftAssignmentStatus.Absent, "ca hôm nay đã hết giờ lúc 23:40");
            int before = await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().CountAsync(a => a.AssignmentStatus == ShiftAssignmentStatus.Absent));
            (await c.PostFormAsync("/api/Job/Run", ("jobCode", "MarkAbsent"))).Ok();
            (await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().CountAsync(a => a.AssignmentStatus == ShiftAssignmentStatus.Absent))).Should().Be(before);
        }
    }
}
