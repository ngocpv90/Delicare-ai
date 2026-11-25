using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Hr
{
    /// <summary>
    /// UI-6.39 (D-120): đơn đổi / nhường ca chờ người nhận đồng ý (chưa có yêu cầu duyệt) → đồng ý tạo yêu cầu duyệt; từ chối / quá hạn → đóng + báo người xin;
    /// phép năm còn = 12 − đã duyệt trong năm (năm trước không tính); PreviewLeave trả ca bị bỏ + tổ thiếu theo ShiftMinStaff; danh sách gộp.
    /// </summary>
    public class LeaveSwapUi639Tests
    {
        private static readonly DateTime Now = new(2026, 12, 10, 9, 0, 0);

        private sealed record World(Guid Unit, Guid Shift, Guid E1, Guid E2, Guid E3, Guid A1, Guid A2);

        private static async Task<World> SeedAsync(InMemoryApiFactory f)
        {
            var w = new World(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            DateTime d1 = Now.Date.AddDays(2), d2 = Now.Date.AddDays(3);
            await f.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = w.Unit, Code = "T639", Name = "Tổ 639" });
                db.Shift.Add(new Shift { Id = w.Shift, Code = "S639", Name = "Sáng 639", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                int i = 0;
                foreach (var e in new[] { w.E1, w.E2, w.E3 })
                {
                    i++;
                    db.Employee.Add(new Employee { Id = e, Code = "nv639-" + i, EmployeeCode = "nv639-" + i, FullName = "NV 639 " + i, OrganizationUnitId = w.Unit, Status = "Active" });
                    db.User.Add(new User { Id = Guid.NewGuid(), UserName = "nv639-" + i, IsActive = true });
                }
                db.ShiftAssignment.Add(new ShiftAssignment { Id = w.A1, EmployeeId = w.E1, ShiftId = w.Shift, WorkDate = d1, PlannedStart = d1.AddHours(6), PlannedEnd = d1.AddHours(14), AssignmentStatus = "Planned" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = w.A2, EmployeeId = w.E2, ShiftId = w.Shift, WorkDate = d2, PlannedStart = d2.AddHours(6), PlannedEnd = d2.AddHours(14), AssignmentStatus = "Planned" });
                // Phép năm: 3 ngày năm nay (đã duyệt) + 5 ngày năm trước (không tính) + 2 ngày chờ duyệt (không tính).
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP639-1", EmployeeId = w.E1, FromDate = new DateTime(2026, 3, 2), ToDate = new DateTime(2026, 3, 4), LeaveTypeCode = "ANNUAL", RequestStatus = "Approved" });
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP639-2", EmployeeId = w.E1, FromDate = new DateTime(2025, 5, 1), ToDate = new DateTime(2025, 5, 5), LeaveTypeCode = "ANNUAL", RequestStatus = "Approved" });
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP639-3", EmployeeId = w.E1, FromDate = new DateTime(2026, 12, 20), ToDate = new DateTime(2026, 12, 21), LeaveTypeCode = "ANNUAL", RequestStatus = "Pending" });
                return Task.CompletedTask;
            });
            return w;
        }

        private static async Task<Guid> UserId(InMemoryApiFactory f, string name) => await f.QueryAsync(db => db.User.AsNoTracking().Where(u => u.UserName == name).Select(u => u.Id).SingleAsync());

        private static async Task<string> PostJson(HttpClient c, string url, object body) => await (await c.PostAsJsonAsync(url, body)).Content.ReadAsStringAsync();

        [Fact]
        public async Task Swap_waits_for_receiver_consent_then_creates_approval_and_reject_or_expiry_closes_it()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            var w = await SeedAsync(f);
            using var admin = f.CreateAuthorizedClient();
            using var e2 = f.CreateAuthorizedClient(await UserId(f, "nv639-2"));
            using var e3 = f.CreateAuthorizedClient(await UserId(f, "nv639-3"));

            var swap = (await admin.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", w.A1), ("ProposedShiftAssignmentId", w.A2), ("Reason", "Bận việc nhà"))).Ok();
            Guid id = swap.GetProperty("request").GetProperty("id").GetGuid();
            swap.GetProperty("request").GetProperty("requestStatus").GetString().Should().Be(ShiftSwapStatus.AwaitingConsent);
            (await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().AnyAsync(a => a.BusinessId == id))).Should().BeFalse("chưa có yêu cầu duyệt khi người nhận chưa đồng ý");
            (await PostJson(e3, "/api/ShiftSwapRequest/Consent", new { id, accept = true })).Should().Contain("Chỉ người nhận");
            (await PostJson(e2, "/api/ShiftSwapRequest/Consent", new { id, accept = true })).Should().Contain("\"responseCode\":200");
            (await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().CountAsync(a => a.BusinessId == id && a.BusinessType == "SHIFT_SWAP"))).Should().Be(1);
            (await f.QueryAsync(db => db.ShiftSwapRequest.AsNoTracking().Where(s => s.Id == id).Select(s => s.RequestStatus).SingleAsync())).Should().Be("Pending");

            // Nhường ca: người nhận từ chối → Từ chối.
            DateTime d4 = Now.Date.AddDays(4);
            Guid a3 = Guid.NewGuid();
            await f.SeedAsync(db => { db.ShiftAssignment.Add(new ShiftAssignment { Id = a3, EmployeeId = w.E1, ShiftId = w.Shift, WorkDate = d4, PlannedStart = d4.AddHours(6), PlannedEnd = d4.AddHours(14), AssignmentStatus = "Planned" }); return Task.CompletedTask; });
            Guid give = (await admin.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a3), ("TargetEmployeeId", w.E3), ("Reason", "Nhường ca"))).Ok().GetProperty("request").GetProperty("id").GetGuid();
            (await PostJson(e3, "/api/ShiftSwapRequest/Consent", new { id = give, accept = false, note = "Bận" })).Should().Contain("\"responseCode\":200");
            (await f.QueryAsync(db => db.ShiftSwapRequest.AsNoTracking().Where(s => s.Id == give).Select(s => s.RequestStatus).SingleAsync())).Should().Be("Rejected");

            // Quá hạn 24 giờ → tự hủy (chạy 2 lần không làm thêm).
            DateTime d5 = Now.Date.AddDays(5);
            Guid a4 = Guid.NewGuid();
            await f.SeedAsync(db => { db.ShiftAssignment.Add(new ShiftAssignment { Id = a4, EmployeeId = w.E1, ShiftId = w.Shift, WorkDate = d5, PlannedStart = d5.AddHours(6), PlannedEnd = d5.AddHours(14), AssignmentStatus = "Planned" }); return Task.CompletedTask; });
            Guid late = (await admin.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a4), ("TargetEmployeeId", w.E3), ("Reason", "Nhường ca 2"))).Ok().GetProperty("request").GetProperty("id").GetGuid();
            f.Clock.FixedNow = Now.AddHours(25);
            for (int i = 0; i < 2; i++)
            {
                using var scope = f.Services.CreateScope();
                using (scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(Guid.NewGuid()), "job-639"))
                {
                    int n = await scope.ServiceProvider.GetRequiredService<AppDelicare.Services.Interface.IShiftSwapRequestService>().ExpireConsentsAsync();
                    n.Should().Be(i == 0 ? 1 : 0);
                }
            }
            (await f.QueryAsync(db => db.ShiftSwapRequest.AsNoTracking().Where(s => s.Id == late).Select(s => s.RequestStatus).SingleAsync())).Should().Be("Cancelled");
        }

        [Fact]
        public async Task Annual_leave_remaining_and_preview_show_dropped_shifts_and_team_shortage()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            f.Settings["BusinessRules:Hr:ShiftMinStaff:T639:S639"] = "2";
            var w = await SeedAsync(f);
            DateTime d1 = Now.Date.AddDays(2);
            await f.SeedAsync(db => { db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = w.E2, ShiftId = w.Shift, WorkDate = d1, PlannedStart = d1.AddHours(6), PlannedEnd = d1.AddHours(14), AssignmentStatus = "Planned" }); return Task.CompletedTask; });
            using var admin = f.CreateAuthorizedClient();
            var p = (await admin.GetResultAsync($"/api/HrRequest/PreviewLeave?EmployeeId={w.E1}&FromDate={d1:yyyy-MM-dd}&ToDate={d1:yyyy-MM-dd}&LeaveTypeCode=ANNUAL")).Ok();
            p.GetProperty("annualTotal").GetInt32().Should().Be(12);
            p.GetProperty("annualUsed").GetInt32().Should().Be(3, "đơn năm trước và đơn chờ duyệt không tính");
            p.GetProperty("annualAfter").GetInt32().Should().Be(8);
            p.GetProperty("listShiftDrop").GetArrayLength().Should().Be(1);
            var st = p.GetProperty("listStaffing")[0];
            st.GetProperty("remaining").GetInt32().Should().Be(1);
            st.GetProperty("required").GetInt32().Should().Be(2);
            st.GetProperty("short").GetBoolean().Should().BeTrue();
            p.GetProperty("listReplacement")[0].GetProperty("listPerson").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Contain(w.E3).And.NotContain(w.E2);

            var leave = (await admin.PostFormAsync("/api/LeaveRequest/Create", ("EmployeeId", w.E1), ("FromDate", d1.ToString("yyyy-MM-dd")), ("ToDate", d1.ToString("yyyy-MM-dd")), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Việc riêng"))).Ok();
            leave.GetProperty("impact").GetProperty("annualRemaining").GetInt32().Should().Be(9);
            var list = (await admin.GetResultAsync("/api/HrRequest/GetList")).ListData.EnumerateArray().ToList();
            list.Should().Contain(x => x.GetProperty("type").GetString() == "LEAVE" && x.GetProperty("listImpact").EnumerateArray().Any(i => i.GetString()!.Contains("Tổ thiếu")));
            (await admin.GetResultAsync("/api/HrRequest/GetSummary")).Ok().GetProperty("pendingLeave").GetInt32().Should().BeGreaterThan(0);
            (await admin.GetResultAsync("/api/HrRequest/GetCalendar")).Ok().GetProperty("listDay").GetArrayLength().Should().Be(14);
        }
    }
}
