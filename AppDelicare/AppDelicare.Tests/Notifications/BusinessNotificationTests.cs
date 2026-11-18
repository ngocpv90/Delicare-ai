using System.Text.Json;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-1.7 (BOT_TELEGRAM §5.1): thông báo nghiệp vụ qua outbox – APPROVAL_PENDING tới đúng người duyệt của cấp (không kèm admin / người gửi), chuyển cấp báo cấp sau,
    /// APPROVAL_RESULT về người gửi; SHIFT_CONFIRMED mỗi nhân viên 1 tin gom ca, gọi lại không gửi trùng, notificationSent đúng; bot đích theo phạm vi
    /// (duyệt → DH, người không có DH → VH); mẫu tin: mọi mã trong danh mục có câu riêng, nút trong tin hiển thị (bỏ nút sai).
    /// </summary>
    public class BusinessNotificationTests
    {
        private static readonly DateTime Now = new(2026, 12, 10, 9, 0, 0);
        private static readonly Guid Vh = Guid.Parse("66666666-0000-4000-8000-0000000000a1");
        private static readonly Guid Dh = Guid.Parse("66666666-0000-4000-8000-0000000000a2");

        private static async Task SeedBotsAsync(InMemoryApiFactory f)
            => await f.SeedAsync(db =>
            {
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" },
                    new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                return Task.CompletedTask;
            });

        private static Task LinkAsync(InMemoryApiFactory f, Guid channel, string external, Guid? user = null, Guid? employee = null)
            => f.SeedAsync(db =>
            {
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, EmployeeId = employee, IntegrationChannelId = channel, ExternalUserId = external, LinkedAt = Now, IdentityStatus = "Active" });
                return Task.CompletedTask;
            });

        /// <summary>User thuộc 1 nhóm có quyền Duyệt trên "#/wf-inbox?type=&lt;screen&gt;" + toàn quyền trên các màn "#/&lt;screen&gt;".</summary>
        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string[] approve, string[]? edit = null)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "nt-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "NT-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (var link in approve.Select(s => "#/wf-inbox?type=" + s).Concat((edit ?? []).Select(s => "#/" + s)))
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole
                    {
                        Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true, AllowDelete = true,
                        AllowPrint = true, AllowExport = true, AllowImport = true,
                    });
                }
            });
            return userId;
        }

        private static async Task<Guid> DraftPurchaseRequestAsync(InMemoryApiFactory f, string code)
        {
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), supplier = Guid.NewGuid(), pr = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = meat, Code = "THIT-" + code, Name = "Thịt " + code, BaseUnitId = kg });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "S-" + code, Name = "NCC " + code, Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = "PH-" + code, RequestNo = "PH-" + code, RequestDate = Now.Date, RequiredDate = Now.Date.AddDays(2), Status = "Draft" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = meat, Quantity = 10, UnitId = kg, UomId = kg });
                return Task.CompletedTask;
            });
            return pr;
        }

        private static Task<List<OutboundMessage>> OutboxAsync(InMemoryApiFactory f, string template)
            => f.QueryAsync(db => db.OutboundMessage.AsNoTracking().Where(m => m.MessageType == template).ToListAsync());

        [Fact]
        public async Task Approval_pending_goes_to_step_approvers_then_next_step_then_result_to_requester()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            await SeedBotsAsync(f);
            Guid requesterId = await UserAsync(f, [], ["scm-pr"]);
            Guid headId = await UserAsync(f, ["scm-pr-l1"]);
            Guid head2Id = await UserAsync(f, ["scm-pr-l1"]);
            Guid cooId = await UserAsync(f, ["scm-pr-l2"]);
            await LinkAsync(f, Vh, "9001", requesterId);
            await LinkAsync(f, Vh, "9002", headId);          // trưởng phòng chỉ có bot VH → nhận ở VH
            await LinkAsync(f, Dh, "9003", cooId);           // COO có DH → tin duyệt (phạm vi Duyệt) ở DH
            await LinkAsync(f, Vh, "9013", cooId);
            await LinkAsync(f, Vh, "9099", InMemoryApiFactory.UserId); // admin / người khác không có quyền duyệt cấp → không nhận
            Guid pr = await DraftPurchaseRequestAsync(f, "NT1");
            using var requester = f.CreateAuthorizedClient(requesterId);
            using var head = f.CreateAuthorizedClient(headId);
            using var coo = f.CreateAuthorizedClient(cooId);

            (await requester.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            var pending = await OutboxAsync(f, NotificationCatalog.ApprovalPending);
            pending.Select(m => m.RecipientExternalId).Should().BeEquivalentTo(["9002"], "chỉ người duyệt cấp 1 đã liên kết bot (head2 chưa liên kết, admin / người gửi không nhận)");
            pending[0].IntegrationChannelId.Should().Be(Vh);
            pending[0].MessageStatus.Should().Be("Queued");
            var card = MessageTemplates.Render(pending[0].PayloadJson);
            card.Title.Should().StartWith("⏳ Chờ bạn duyệt: XN-");
            card.Lines.Should().Contain(l => l.Contains("PH-NT1")).And.Contain(l => l.StartsWith("Cấp: "));
            var request = await f.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == pr));
            card.Buttons.Should().ContainSingle().Which.Callback.Should().Be("apv:view:" + request.Id.ToString("N"));

            (await head.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Ok();
            pending = await OutboxAsync(f, NotificationCatalog.ApprovalPending);
            pending.Where(m => m.RecipientExternalId == "9003").Should().ContainSingle("cấp 2 → COO ở bot DH").Which.IntegrationChannelId.Should().Be(Dh);
            pending.Should().NotContain(m => m.RecipientExternalId == "9013", "1 sự kiện không tới 2 bot của cùng một người");
            (await OutboxAsync(f, NotificationCatalog.ApprovalResult)).Should().BeEmpty("chưa có kết quả cuối");

            (await coo.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr), ("Note", "Đồng ý"))).Ok();
            var result = (await OutboxAsync(f, NotificationCatalog.ApprovalResult)).Should().ContainSingle().Subject;
            result.RecipientExternalId.Should().Be("9001");
            result.IntegrationChannelId.Should().Be(Vh, "kết quả là tin cá nhân → bot VH");
            var rendered = MessageTemplates.Render(result.PayloadJson);
            rendered.Title.Should().StartWith("✅ Đã duyệt: XN-");
            rendered.Lines.Should().Contain("Ghi chú: Đồng ý");
        }

        [Fact]
        public async Task Shift_confirm_sends_one_message_per_employee_without_duplicates()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            await SeedBotsAsync(f);
            Guid hr = await UserAsync(f, [], ["hr-shift-assign"]);
            Guid shift = Guid.NewGuid(), unit = Guid.NewGuid(), linked = Guid.NewGuid(), notLinked = Guid.NewGuid();
            DateTime day = Now.Date.AddDays(1);
            await f.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = unit, Code = "BEP-NT", Name = "Bếp NT" });
                db.Shift.Add(new Shift { Id = shift, Code = "SANG-NT", Name = "Ca sáng", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0) });
                foreach (var e in new[] { linked, notLinked })
                {
                    db.Employee.Add(new Employee { Id = e, Code = "NV-" + e.ToString("N")[..4], FullName = "NV " + e.ToString("N")[..4], OrganizationUnitId = unit, Status = "Active" });
                    for (int i = 0; i < 2; i++)
                    {
                        db.ShiftAssignment.Add(new ShiftAssignment
                        {
                            Id = Guid.NewGuid(), EmployeeId = e, ShiftId = shift, WorkDate = day.AddDays(i), PlannedStart = day.AddDays(i).AddHours(6), PlannedEnd = day.AddDays(i).AddHours(14),
                            AssignmentStatus = "Planned",
                        });
                    }
                }
                return Task.CompletedTask;
            });
            await LinkAsync(f, Vh, "9101", employee: linked);
            using var client = f.CreateAuthorizedClient(hr);

            var confirmed = (await client.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", day.ToString("yyyy-MM-dd")), ("ToDate", day.AddDays(1).ToString("yyyy-MM-dd")),
                ("OrganizationUnitId", unit))).Ok();
            confirmed.GetProperty("confirmed").GetInt32().Should().Be(4);
            confirmed.GetProperty("notificationSent").GetBoolean().Should().BeTrue();
            var msg = (await OutboxAsync(f, NotificationCatalog.ShiftConfirmed)).Should().ContainSingle("1 tin / nhân viên đã liên kết; người chưa liên kết bỏ qua").Subject;
            msg.RecipientExternalId.Should().Be("9101");
            var rendered = MessageTemplates.Render(msg.PayloadJson);
            rendered.Title.Should().Be("📅 Lịch ca đã xác nhận (2 ca)");
            rendered.Lines.Should().Equal($"{day:dd/MM} Ca sáng 06:00–14:00", $"{day.AddDays(1):dd/MM} Ca sáng 06:00–14:00");

            var again = (await client.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", day.ToString("yyyy-MM-dd")), ("ToDate", day.AddDays(1).ToString("yyyy-MM-dd")),
                ("OrganizationUnitId", unit))).Ok();
            again.GetProperty("confirmed").GetInt32().Should().Be(0);
            again.GetProperty("notificationSent").GetBoolean().Should().BeFalse();
            (await OutboxAsync(f, NotificationCatalog.ShiftConfirmed)).Should().HaveCount(1, "gọi lại không gửi trùng");
        }

        [Fact]
        public void Every_catalog_template_has_its_own_wording_and_buttons_render()
        {
            foreach (var t in NotificationCatalog.All)
            {
                var m = MessageTemplates.Render(JsonSerializer.Serialize(new { template = t.Code, data = new { title = "x" }, level = t.Level }));
                (m.Title ?? string.Join(" ", m.Lines)).Should().NotBe("Có thông báo mới", t.Code + " cần câu riêng");
                m.Level.Should().Be(t.Level);
            }
            NotificationCatalog.BotFor(NotificationCatalog.Find(NotificationCatalog.ApprovalPending)!.Scope).Should().Be("DH");
            NotificationCatalog.BotFor(NotificationCatalog.Find(NotificationCatalog.ShiftConfirmed)!.Scope).Should().Be("VH");
            NotificationCatalog.BotFor(NotificationCatalog.Find(NotificationCatalog.FoodSafetyCritical)!.Scope).Should().Be("DH");

            string longCb = "apv:view:" + new string('a', 60);
            var withButtons = MessageTemplates.Render(JsonSerializer.Serialize(new
            {
                template = "COMPLAINT_NEW", data = new { title = "PA-1", lines = new[] { "Trường A", "Cơm nguội" } },
                buttons = new object[] { new { text = "📄 Xem", callback = "cmp:v:abc" }, new { text = "Dài", callback = longCb }, new { text = "", callback = "x:y" }, new { text = "Web", url = "https://x" } },
            }));
            withButtons.Title.Should().Be("📣 Phản ánh mới");
            withButtons.Lines.Should().Equal("PA-1", "Trường A", "Cơm nguội");
            withButtons.Buttons.Select(b => b.Text).Should().Equal("📄 Xem", "Web");
        }
    }
}
