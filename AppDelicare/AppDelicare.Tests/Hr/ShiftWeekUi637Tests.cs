using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Hr
{
    /// <summary>
    /// UI-6.37 (D-118): phân ca tuần.
    /// - AssignMany 10 ô có 1 ô phép đã duyệt + 1 ô đã vào ca → 8 OK, 2 lỗi đúng lý do, ô lỗi không ghi.
    /// - Đổi / bỏ ô đã chốt → changedAfterConfirm; Confirm gửi đúng 1 SHIFT_CHANGED / người; gọi lại không gửi lại.
    /// - CopyWeek không ghi đè, bỏ qua phép; chạy lần 2 = 0. GetGrid tuần luôn từ Thứ 2, tổng tổ × ca khớp, thiếu theo ShiftMinStaff.
    /// </summary>
    public class ShiftWeekUi637Tests
    {
        // Thứ 4 26/11/2025 9:00 – tuần Thứ 2 24/11.
        private static readonly DateTime Now = new(2025, 11, 26, 9, 0, 0);
        private static readonly Guid Vh = Guid.Parse("66666666-0000-4000-8000-0000000637a1");

        private sealed record World(Guid Unit, Guid Sang, Guid Trua, List<Guid> Employees);

        private static async Task<World> SeedAsync(InMemoryApiFactory f, int people = 3)
        {
            var w = new World(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [.. Enumerable.Range(0, people).Select(_ => Guid.NewGuid())]);
            await f.SeedAsync(db =>
            {
                db.IntegrationChannel.Add(new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH637", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = w.Unit, Code = "BEP637", Name = "Bếp 637" });
                db.Shift.Add(new Shift { Id = w.Sang, Code = "S637", Name = "Sáng 637", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                db.Shift.Add(new Shift { Id = w.Trua, Code = "T637", Name = "Trưa 637", StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(18, 0, 0), Status = "Active" });
                int i = 0;
                foreach (var e in w.Employees)
                {
                    db.Employee.Add(new Employee { Id = e, Code = "NV637-" + i, FullName = "NV 637 " + i++, OrganizationUnitId = w.Unit, Status = "Active" });
                    db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), EmployeeId = e, IntegrationChannelId = Vh, ExternalUserId = "637" + i, LinkedAt = Now, IdentityStatus = "Active" });
                }
                return Task.CompletedTask;
            });
            return w;
        }

        private static object Cell(Guid e, DateTime d, Guid? s) => new { employeeId = e, workDate = d.ToString("yyyy-MM-dd"), shiftId = s };

        private static async Task<JsonElement> JsonOk(HttpResponseMessage r)
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("responseCode").GetInt32().Should().Be(200, doc.RootElement.ToString());
            return doc.RootElement.GetProperty("data").Clone();
        }

        [Fact]
        public async Task Assign_many_saves_valid_cells_and_reports_leave_and_attended_cells()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            var w = await SeedAsync(f, 2);
            DateTime d0 = Now.Date;
            await f.SeedAsync(db =>
            {
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP-637", EmployeeId = w.Employees[0], FromDate = d0.AddDays(2), ToDate = d0.AddDays(2), LeaveTypeCode = "ANNUAL", RequestStatus = LeaveRequestStatus.Approved });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = w.Employees[1], ShiftId = w.Trua, WorkDate = d0, PlannedStart = d0.AddHours(10), PlannedEnd = d0.AddHours(18), AssignmentStatus = ShiftAssignmentStatus.CheckedIn });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            var cells = new List<object>();
            foreach (var e in w.Employees)
            {
                for (int i = 0; i < 5; i++)
                {
                    cells.Add(Cell(e, d0.AddDays(i), w.Sang));
                }
            }
            var data = await JsonOk(await c.PutAsJsonAsync("/api/ShiftAssignment/AssignMany", new { listCell = cells }));
            data.GetProperty("done").GetInt32().Should().Be(8);
            data.GetProperty("failed").GetInt32().Should().Be(2);
            var errors = data.GetProperty("listResult").EnumerateArray().Where(x => !x.GetProperty("ok").GetBoolean()).Select(x => x.GetProperty("message").GetString()).ToList();
            errors.Should().Contain(m => m!.Contains("NP-637")).And.Contain(m => m!.Contains("Đã vào ca"));
            (await f.QueryAsync(db => db.ShiftAssignment.AsNoTracking().CountAsync(a => !a.IsDeleted && a.ShiftId == w.Sang))).Should().Be(8);
        }

        [Fact]
        public async Task Changing_confirmed_cells_marks_them_and_confirm_sends_one_shift_changed_per_person()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            var w = await SeedAsync(f, 2);
            DateTime d1 = Now.Date.AddDays(1);
            using var c = f.CreateAuthorizedClient();
            await JsonOk(await c.PutAsJsonAsync("/api/ShiftAssignment/AssignMany", new { listCell = new[] { Cell(w.Employees[0], d1, w.Sang), Cell(w.Employees[0], d1.AddDays(1), w.Sang), Cell(w.Employees[1], d1, w.Sang) } }));
            (await c.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", d1.ToString("yyyy-MM-dd")), ("ToDate", d1.AddDays(5).ToString("yyyy-MM-dd")))).Ok();

            await JsonOk(await c.PutAsJsonAsync("/api/ShiftAssignment/AssignMany", new { listCell = new[] { Cell(w.Employees[0], d1, w.Trua), Cell(w.Employees[0], d1.AddDays(1), null) } }));
            var grid = (await c.GetResultAsync($"/api/ShiftAssignment/GetGrid?WeekStart={Now:yyyy-MM-dd}&OrganizationUnitId={w.Unit}")).Ok();
            grid.GetProperty("weekStatus").GetProperty("changedAfterConfirmCount").GetInt32().Should().Be(2);
            var row = grid.GetProperty("employees").EnumerateArray().First(e => e.GetProperty("id").GetGuid() == w.Employees[0]);
            row.GetProperty("cells").GetProperty(d1.ToString("yyyy-MM-dd")).GetProperty("changedAfterConfirm").GetBoolean().Should().BeTrue();

            var confirm = (await c.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", d1.ToString("yyyy-MM-dd")), ("ToDate", d1.AddDays(5).ToString("yyyy-MM-dd")))).Ok();
            confirm.GetProperty("changedNotified").GetInt32().Should().Be(1);
            var msgs = await f.QueryAsync(db => db.OutboundMessage.AsNoTracking().Where(m => m.MessageType == NotificationCatalog.ShiftChanged).ToListAsync());
            msgs.Should().ContainSingle();
            var lines = MessageTemplates.Render(msgs[0].PayloadJson).Lines;
            lines.Should().Contain(l => l.Contains("S637 → T637")).And.Contain(l => l.Contains("đã hủy ca"));
            (await c.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", d1.ToString("yyyy-MM-dd")), ("ToDate", d1.AddDays(5).ToString("yyyy-MM-dd")))).Ok()
                .GetProperty("changedNotified").GetInt32().Should().Be(0);
            (await f.QueryAsync(db => db.OutboundMessage.AsNoTracking().CountAsync(m => m.MessageType == NotificationCatalog.ShiftChanged))).Should().Be(1, "gọi lại không gửi lại");
        }

        [Fact]
        public async Task Copy_week_fills_only_empty_cells_skips_leave_and_week_grid_counts_shortage()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            f.Settings["BusinessRules:Hr:ShiftMinStaff:BEP637:S637"] = "3";
            var w = await SeedAsync(f, 2);
            DateTime monday = new(2025, 11, 24), next = monday.AddDays(7);
            await f.SeedAsync(db =>
            {
                foreach (var e in w.Employees)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = e, ShiftId = w.Sang, WorkDate = monday.AddDays(i), PlannedStart = monday.AddDays(i).AddHours(6), PlannedEnd = monday.AddDays(i).AddHours(14), AssignmentStatus = "Confirmed" });
                    }
                }
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = w.Employees[1], ShiftId = w.Trua, WorkDate = next, PlannedStart = next.AddHours(10), PlannedEnd = next.AddHours(18), AssignmentStatus = "Planned" });
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP-637C", EmployeeId = w.Employees[0], FromDate = next.AddDays(1), ToDate = next.AddDays(1), LeaveTypeCode = "ANNUAL", RequestStatus = LeaveRequestStatus.Approved });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            var copy = await JsonOk(await c.PostAsJsonAsync("/api/ShiftAssignment/CopyWeek", new { fromWeekStart = monday, toWeekStart = next.AddDays(3), organizationUnitId = w.Unit }));
            copy.GetProperty("created").GetInt32().Should().Be(4, "6 ô nguồn − 1 ô đã có ca khác − 1 ô phép");
            copy.GetProperty("skipped").GetInt32().Should().Be(2);
            (await JsonOk(await c.PostAsJsonAsync("/api/ShiftAssignment/CopyWeek", new { fromWeekStart = monday, toWeekStart = next, organizationUnitId = w.Unit })))
                .GetProperty("created").GetInt32().Should().Be(0, "chạy lần 2 không ghi thêm");

            var grid = (await c.GetResultAsync($"/api/ShiftAssignment/GetGrid?WeekStart={next.AddDays(4):yyyy-MM-dd}&OrganizationUnitId={w.Unit}")).Ok();
            grid.GetProperty("fromDate").GetDateTime().Should().Be(next, "tuần luôn từ Thứ 2");
            grid.GetProperty("days").GetArrayLength().Should().Be(7);
            var totals = grid.GetProperty("unitTotals").EnumerateArray().Where(t => t.GetProperty("shiftCode").GetString() == "S637").ToList();
            var tue = totals.First(t => t.GetProperty("day").GetString() == next.AddDays(1).ToString("yyyy-MM-dd"));
            tue.GetProperty("assigned").GetInt32().Should().Be(1);
            tue.GetProperty("required").GetInt32().Should().Be(3);
            tue.GetProperty("short").GetBoolean().Should().BeTrue();
            grid.GetProperty("kpi").GetProperty("shortSlots").GetInt32().Should().Be(7, "S637 cần 3 người mọi ngày, tối đa 2 người");
        }

        [Fact]
        public async Task Friday_reminder_for_next_week_drafts_is_sent_once()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = new DateTime(2025, 11, 28, 15, 30, 0); // Thứ 6
            var w = await SeedAsync(f, 2);
            DateTime next = new(2025, 12, 1);
            await f.SeedAsync(async db =>
            {
                (await db.OrganizationUnit.SingleAsync(o => o.Id == w.Unit)).ManagerId = w.Employees[0];
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = w.Employees[1], ShiftId = w.Sang, WorkDate = next, PlannedStart = next.AddHours(6), PlannedEnd = next.AddHours(14), AssignmentStatus = "Planned" });
            });
            for (int i = 0; i < 2; i++)
            {
                using var scope = f.Services.CreateScope();
                using (scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(Guid.NewGuid()), "job-637"))
                {
                    await scope.ServiceProvider.GetRequiredService<AppDelicare.Services.Interface.IShiftAssignmentService>().RemindNextWeekAsync();
                }
            }
            var msgs = await f.QueryAsync(db => db.OutboundMessage.AsNoTracking().Where(m => m.IdempotencyKey != null && m.IdempotencyKey.StartsWith("SHIFT_WEEK_REMIND:MGR")).ToListAsync());
            msgs.Should().ContainSingle("trưởng đơn vị nhận 1 tin / tuần dù job chạy 2 lần");
            JsonDocument.Parse(msgs[0].PayloadJson!).RootElement.GetProperty("data").GetProperty("text").GetString().Should().Contain("1 ca chưa chốt");
        }
    }
}
