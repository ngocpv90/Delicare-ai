using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-2.3 (BOT_TELEGRAM §5.1, §5.3) – phía api, đồng hồ giả:
    /// giờ yên lặng theo ca (🟡 ngoài ca → hẹn tới 60′ trước ca kế tiếp; người không có phân ca → 07:00; 🔴 không tới người ngoài ca trừ khi không còn ai / leo thang);
    /// kiểm tra định kỳ (xe chưa rời, mẻ trễ, phản ánh sắp / quá SLA, duyệt quá hạn) đúng ngưỡng, gọi lại không gửi trùng;
    /// job 19:00 khách chưa chốt → NV KD phụ trách; bản tin sáng → người có Dashboard điều hành ở bot DH, chạy lại không trùng.
    /// </summary>
    public class ScheduledNotificationTests
    {
        private static readonly DateTime Day = new(2026, 12, 16);
        private static readonly Guid Vh = Guid.Parse("99999999-0000-4000-8000-0000000000a1");
        private static readonly Guid Dh = Guid.Parse("99999999-0000-4000-8000-0000000000a2");
        private static readonly Guid SystemUser = Guid.Parse("99999999-0000-4000-8000-0000000000ff");

        private sealed record People(Guid OnDuty, Guid Evening, Guid Office, Guid OnDutyEmp, Guid EveningEmp);

        /// <summary>3 người đã liên kết bot VH: đang trong ca 08–16, ca tối 14–22, văn phòng (không có phân ca). Cùng nhóm quyền với các menu cho trước.</summary>
        private static async Task<People> SeedAsync(InMemoryApiFactory f, params string[] menus)
        {
            var p = new People(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = SystemUser, UserName = "system-23", IsActive = true });
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" },
                    new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                Guid shift = Guid.NewGuid(), group = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = "CA-23", Name = "Ca 23" });
                db.Employee.AddRange(new Employee { Id = p.OnDutyEmp, Code = "NV-23A", FullName = "Bếp A", Status = "Active" },
                    new Employee { Id = p.EveningEmp, Code = "NV-23B", FullName = "Bếp B", Status = "Active" });
                db.ShiftAssignment.AddRange(
                    new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = p.OnDutyEmp, ShiftId = shift, WorkDate = Day, PlannedStart = Day.AddHours(8), PlannedEnd = Day.AddHours(16), AssignmentStatus = "Confirmed" },
                    new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = p.EveningEmp, ShiftId = shift, WorkDate = Day, PlannedStart = Day.AddHours(14), PlannedEnd = Day.AddHours(22), AssignmentStatus = "Confirmed" });
                db.User.AddRange(new User { Id = p.OnDuty, UserName = "u23-a", IsActive = true }, new User { Id = p.Evening, UserName = "u23-b", IsActive = true },
                    new User { Id = p.Office, UserName = "u23-office", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "G-23" });
                foreach (var u in new[] { p.OnDuty, p.Evening, p.Office })
                {
                    db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = group });
                }
                foreach (string link in menus)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                    if (db.Entry(menu).State == EntityState.Detached)
                    {
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                }
                db.UserExternalIdentity.AddRange(
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = p.OnDuty, EmployeeId = p.OnDutyEmp, IntegrationChannelId = Vh, ExternalUserId = "2301", LinkedAt = Day, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = p.Evening, EmployeeId = p.EveningEmp, IntegrationChannelId = Vh, ExternalUserId = "2302", LinkedAt = Day, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = p.Office, IntegrationChannelId = Vh, ExternalUserId = "2303", LinkedAt = Day, IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = p.Office, IntegrationChannelId = Dh, ExternalUserId = "2304", LinkedAt = Day, IdentityStatus = "Active" });
            });
            return p;
        }

        private static InMemoryApiFactory NewFactory(bool quiet = true)
        {
            var f = new InMemoryApiFactory();
            f.Settings["SystemUsers:JobUserId"] = SystemUser.ToString();
            f.Settings["Messaging:QuietHours:Enabled"] = quiet ? "true" : "false";
            f.Settings["Agent:TemplateBriefings"] = "false"; // AI-25.4: test bản tin bot cũ (MorningBrief / DailyReport) – tắt bản tin mẫu thay thế
            return f;
        }

        private static async Task EnqueueAsync(InMemoryApiFactory f, string template, string key, bool escalation, params Guid[] users)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(SystemUser));
            await scope.ServiceProvider.GetRequiredService<INotificationService>().EnqueueAsync(new NotificationRequest
            {
                Template = template, UserIds = [.. users], Data = new { title = "Thử" }, IdempotencyKey = key, Escalation = escalation, BotCode = "VH",
            });
        }

        /// <summary>Người nhận (ExternalUserId) → notBefore của tin theo khóa.</summary>
        private static async Task<Dictionary<string, string?>> SentAsync(InMemoryApiFactory f, string key)
            => (await f.QueryAsync(db => db.OutboundMessage.Where(m => m.IdempotencyKey!.StartsWith(key + ":")).ToListAsync()))
                .ToDictionary(m => m.RecipientExternalId!, m => JsonDocument.Parse(m.PayloadJson!).RootElement.GetProperty("notBefore") is { ValueKind: JsonValueKind.String } nb ? nb.GetString() : null);

        [Fact]
        public async Task Quiet_hours_follow_each_recipients_shift()
        {
            using var f = NewFactory();
            var p = await SeedAsync(f);

            f.Clock.FixedNow = Day.AddHours(9);
            await EnqueueAsync(f, NotificationCatalog.ChecklistDue, "Q1", false, p.OnDuty, p.Evening, p.Office);
            var normal = await SentAsync(f, "Q1");
            normal["2301"].Should().BeNull("đang trong ca → gửi ngay");
            normal["2302"].Should().Be("2026-12-16T13:00:00", "ngoài ca → hẹn 60′ trước ca 14:00");
            normal["2303"].Should().BeNull("không có phân ca, trong giờ hành chính");

            await EnqueueAsync(f, NotificationCatalog.BatchLate, "Q2", false, p.OnDuty, p.Evening, p.Office);
            (await SentAsync(f, "Q2")).Keys.Should().BeEquivalentTo(["2301", "2303"], "🔴 không tới người có phân ca đang ngoài ca");
            await EnqueueAsync(f, NotificationCatalog.BatchLate, "Q3", false, p.Evening);
            (await SentAsync(f, "Q3")).Keys.Should().Equal(["2302"], "không còn ai khác → vẫn gửi, không để mất tin khẩn");

            f.Clock.FixedNow = Day.AddHours(22).AddMinutes(30);
            await EnqueueAsync(f, NotificationCatalog.ChecklistDue, "Q4", false, p.Office);
            (await SentAsync(f, "Q4"))["2303"].Should().Be("2026-12-17T07:00:00", "người không có phân ca: ngoài 07–21 → 07:00 hôm sau");
            await EnqueueAsync(f, NotificationCatalog.ChecklistDue, "Q5", true, p.Office, p.Evening);
            (await SentAsync(f, "Q5")).Values.Should().AllSatisfy(v => v.Should().BeNull("tin leo thang không xét giờ yên lặng"));
        }

        private static async Task<int> WatchAsync(InMemoryApiFactory f)
        {
            using var scope = f.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<IBotWatchService>().Run()).Data;
        }

        private static Task<List<OutboundMessage>> OfAsync(InMemoryApiFactory f, string template)
            => f.QueryAsync(db => db.OutboundMessage.Where(m => m.MessageType == template).ToListAsync());

        [Fact]
        public async Task Watch_reports_late_trip_batch_complaint_and_overdue_approval_once()
        {
            using var f = NewFactory(quiet: false);
            var p = await SeedAsync(f, "#/log-route", "#/prod-batch", "#/log-complaint", "#/wf-inbox?type=" + LeaveRequestStatus.ScreenId);
            Guid late = Guid.NewGuid(), soon = Guid.NewGuid(), batch = Guid.NewGuid(), complaint = Guid.NewGuid(), customer = Guid.NewGuid(), leave = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.DeliveryRoute.AddRange(
                    new DeliveryRoute { Id = late, RouteNo = "CX-23A", DeliveryDate = Day, PlannedStart = Day.AddHours(9), Status = DeliveryRouteStatus.Planned },
                    new DeliveryRoute { Id = soon, RouteNo = "CX-23B", DeliveryDate = Day, PlannedStart = Day.AddHours(9).AddMinutes(10), Status = DeliveryRouteStatus.Planned });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, BatchNo = "ME-23", ProductionOrderId = Guid.NewGuid(), DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(),
                    StartDate = Day.AddHours(8).AddMinutes(50), EndDate = Day.AddHours(10), Status = ProductionBatchStatus.Planned });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-23", Name = "Khách 23", CIF = "KH-23", DisplayName = "Khách 23", CustomerType = "FACTORY", Status = "Active" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = complaint, ComplaintNo = "PA-23", CustomerId = customer, Description = "Cơm nguội", ReceivedAt = Day.AddHours(7), SlaDueAt = Day.AddHours(9).AddMinutes(50), ComplaintStatus = ComplaintStatus.Open });
                db.LeaveRequest.Add(new LeaveRequest { Id = leave, RequestNo = "NP-23", EmployeeId = p.OnDutyEmp, LeaveTypeCode = "ANNUAL", FromDate = Day.AddDays(3), ToDate = Day.AddDays(3), RequestStatus = "Pending", SubmittedAt = Day.AddDays(-2) });
                db.ApprovalRequest.Add(new ApprovalRequest { Id = Guid.NewGuid(), RequestNo = "YC-23", BusinessType = LeaveRequestStatus.BusinessType, BusinessId = leave, RequestedBy = p.Evening,
                    RequestedAt = Day.AddDays(-2), RequestStatus = "Pending", CurrentStepNo = 1, DueAt = Day.AddDays(-1).AddHours(8) });
                return Task.CompletedTask;
            });

            f.Clock.FixedNow = Day.AddHours(9).AddMinutes(15);
            (await WatchAsync(f)).Should().BeGreaterThan(0);
            var trips = await OfAsync(f, NotificationCatalog.TripNotDeparted);
            trips.Should().NotBeEmpty();
            trips.Select(m => MessageTemplates.Render(m.PayloadJson).Lines.Concat([MessageTemplates.Render(m.PayloadJson).Title ?? ""])).SelectMany(x => x)
                .Should().Contain(l => l.Contains("CX-23A")).And.NotContain(l => l.Contains("CX-23B"), "chuyến mới trễ 5′ – chưa tới ngưỡng 10′");
            MessageTemplates.Render(trips[0].PayloadJson).Buttons.Select(b => b.Callback).Should().Contain("inc:claim", "xe chưa rời = sự việc (nhận xử lý / leo thang)");
            (await OfAsync(f, NotificationCatalog.BatchLate)).Should().NotBeEmpty("mẻ kế hoạch 08:50 chưa bắt đầu sau 25′");
            var sla = await OfAsync(f, NotificationCatalog.ComplaintSla);
            sla.Should().OnlyContain(m => m.IdempotencyKey!.Contains(":warn:"), "còn 35′ tới hạn → nhắc sắp quá hạn");
            var overdue = await OfAsync(f, NotificationCatalog.ApprovalOverdue);
            overdue.Select(m => m.RecipientExternalId).Should().BeEquivalentTo(["2301", "2304"], "người duyệt của cấp (tin duyệt → bot DH nếu có), trừ người gửi (2302)");

            int total = await f.QueryAsync(db => db.OutboundMessage.CountAsync());
            (await WatchAsync(f)).Should().Be(0, "gọi lại không gửi trùng");
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync())).Should().Be(total);

            f.Clock.FixedNow = Day.AddHours(10);
            await WatchAsync(f);
            (await OfAsync(f, NotificationCatalog.ComplaintSla)).Should().Contain(m => m.IdempotencyKey!.Contains(":over:"), "quá hạn SLA → báo lần 2");
            (await OfAsync(f, NotificationCatalog.TripNotDeparted)).Should().Contain(m => m.PayloadJson!.Contains("CX-23B"), "10:00 – chuyến 09:10 đã trễ quá 10′");
        }

        /// <summary>BIZ-1.6 (D-77 f): duyệt quá hạn không bị khóa, chỉ leo thang – quá hạn → người duyệt; +24 giờ → COO; +48 giờ → CEO (nhóm cấu hình ApprovalOverdueTopGroups).</summary>
        [Fact]
        public async Task Overdue_approval_escalates_to_top_group_after_twice_the_escalation_hours()
        {
            using var f = NewFactory(quiet: false);
            f.Settings["Messaging:Watch:ApprovalOverdueGroups:0"] = "NHOM-KHONG-CO";
            f.Settings["Messaging:Watch:ApprovalOverdueTopGroups:0"] = "G-23";
            var p = await SeedAsync(f, "#/wf-inbox?type=" + LeaveRequestStatus.ScreenId);
            Guid leave = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.LeaveRequest.Add(new LeaveRequest { Id = leave, RequestNo = "NP-24", EmployeeId = p.OnDutyEmp, LeaveTypeCode = "ANNUAL", FromDate = Day.AddDays(3), ToDate = Day.AddDays(3), RequestStatus = "Pending", SubmittedAt = Day.AddDays(-4) });
                db.ApprovalRequest.Add(new ApprovalRequest { Id = Guid.NewGuid(), RequestNo = "YC-24", BusinessType = LeaveRequestStatus.BusinessType, BusinessId = leave, RequestedBy = p.Evening,
                    RequestedAt = Day.AddDays(-4), RequestStatus = "Pending", CurrentStepNo = 1, DueAt = Day.AddDays(-1).AddHours(8) });
                return Task.CompletedTask;
            });
            f.Clock.FixedNow = Day.AddHours(9);
            await WatchAsync(f);
            (await OfAsync(f, NotificationCatalog.ApprovalOverdue)).Should().NotContain(m => m.IdempotencyKey!.Contains(":1:3:"), "mới quá hạn 25 giờ – chưa tới bậc 3 (48 giờ)");
            f.Clock.FixedNow = Day.AddDays(1).AddHours(9);
            await WatchAsync(f);
            (await OfAsync(f, NotificationCatalog.ApprovalOverdue)).Should().Contain(m => m.IdempotencyKey!.Contains(":1:3:"), "quá hạn 49 giờ → báo nhóm bậc 3 (CEO)");
        }

        private static async Task<long> RunJobAsync(InMemoryApiFactory f, string code)
        {
            using var scope = f.Services.CreateScope();
            var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(j => j.Code == code);
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(SystemUser));
            return (await job.RunAsync(f.Clock.Now, CancellationToken.None)).RowsProcessed;
        }

        [Fact]
        public async Task Unconfirmed_orders_at_19h_go_to_the_assigned_sales_person_once()
        {
            using var f = NewFactory(quiet: false);
            var p = await SeedAsync(f);
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.AddRange(
                    new Customer { Id = a, Code = "KH-23A", Name = "Khách A", CIF = "KH-23A", DisplayName = "Khách A", CustomerType = "FACTORY", Status = "Active", UserAssignId = p.Office },
                    new Customer { Id = b, Code = "KH-23B", Name = "Khách B", CIF = "KH-23B", DisplayName = "Khách B", CustomerType = "FACTORY", Status = "Active", UserAssignId = p.Office });
                db.Order.AddRange(
                    new Order { Id = Guid.NewGuid(), OrderNo = "DH-23A", CustomerId = a, OrderDate = Day, MealDate = Day.AddDays(1), DeliveryDate = Day.AddDays(1), Status = OrderStatus.Submitted, TotalQuantity = 120 },
                    new Order { Id = Guid.NewGuid(), OrderNo = "DH-23B", CustomerId = b, OrderDate = Day, MealDate = Day.AddDays(1), DeliveryDate = Day.AddDays(1), Status = OrderStatus.Confirmed, TotalQuantity = 80 });
                return Task.CompletedTask;
            });
            f.Clock.FixedNow = Day.AddHours(19);
            (await RunJobAsync(f, "RemindUnconfirmedOrders")).Should().Be(1);
            var m = (await OfAsync(f, NotificationCatalog.OrdersUnconfirmed)).Should().ContainSingle().Subject;
            m.RecipientExternalId.Should().Be("2303", "NV KD phụ trách khách, bot VH");
            var text = MessageTemplates.Render(m.PayloadJson);
            text.Lines.Should().Contain(l => l.Contains("Khách A") && l.Contains("DH-23A")).And.NotContain(l => l.Contains("Khách B"), "đơn đã xác nhận không nhắc");
            (await RunJobAsync(f, "RemindUnconfirmedOrders")).Should().Be(0, "chạy lại không gửi trùng");
        }

        [Fact]
        public async Task Morning_brief_goes_to_executive_dashboard_users_on_dh_once()
        {
            using var f = NewFactory();
            var p = await SeedAsync(f, BotReports.ExecutiveMenu);
            f.Clock.FixedNow = Day.AddHours(7);
            await RunJobAsync(f, "MorningBrief");
            var briefs = await OfAsync(f, NotificationCatalog.MorningBrief);
            briefs.Should().Contain(m => m.RecipientExternalId == "2304" && m.IntegrationChannelId == Dh, "người có bot DH nhận ở DH");
            var brief = MessageTemplates.Render(briefs.First(m => m.RecipientExternalId == "2304").PayloadJson);
            brief.Title.Should().Contain("Bản tin sáng");
            brief.Lines.Should().Contain(l => l.StartsWith("📅 Hôm qua")).And.Contain(l => l.StartsWith("☀️ Hôm nay"));
            (await SentAsync(f, NotificationCatalog.MorningBrief + ":" + Day.ToString("yyyyMMdd")))["2302"]
                .Should().Be("2026-12-16T13:00:00", "⚪ tới người có phân ca đang ngoài ca → hẹn 60′ trước ca, không gửi lúc 07:00");
            int count = briefs.Count;
            await RunJobAsync(f, "MorningBrief");
            (await OfAsync(f, NotificationCatalog.MorningBrief)).Should().HaveCount(count, "chạy lại không gửi trùng");
        }
    }
}
