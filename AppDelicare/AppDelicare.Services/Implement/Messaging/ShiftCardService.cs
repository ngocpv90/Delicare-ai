using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;

using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>
    /// BOT-2.1 (BOT_TELEGRAM §1.1): Thẻ ca – 1 tin / người / ca, gửi <c>LeadMinutes</c> (mặc định 15′, <c>Messaging:ShiftCard:LeadMinutes</c>) trước giờ vào ca qua outbox
    /// (chống trùng <c>SHIFT_CARD:&lt;ca&gt;</c>, CorrelationId <c>SHIFTCARD:&lt;ca&gt;</c>), messaging ghim + sửa tin khi nội dung đổi (≤ 1 lần / 5′).
    /// Nội dung: ca, giờ, đã / chưa vào ca, chuyến của tài xế hôm nay; nút theo giai đoạn (trước ca / trong ca / cuối ca) lọc theo quyền menu của người đó.
    /// Không bảng / cột mới: thẻ đã gửi = OutboundMessage Sent theo CorrelationId.
    /// </summary>
    public class ShiftCardService(IUnitOfWork unitOfWork, IClock clock, IConfiguration configuration, INotificationService notifications, IPermissionService permissionService,
        ICurrentActor currentActor)
        : IShiftCardService
    {
        public const string Correlation = "SHIFTCARD:";
        private static readonly string[] Open = [ShiftAssignmentStatus.Planned, ShiftAssignmentStatus.Confirmed, ShiftAssignmentStatus.CheckedIn];

        /// <summary>Nút theo giai đoạn: (mã chức năng bot, nhãn, mục menu cần có – null = ai cũng dùng).</summary>
        private static readonly Dictionary<string, (string Code, string Label, string? Menu)[]> PhaseButtons = new()
        {
            ["BEFORE"] = [("check_in", "📍 Chấm công", null), ("hr_absent", "👥 Ai chưa vào", "#/hr-shift-assign"), ("bep_batches", "📋 Kế hoạch ca", "#/prod-batch"), ("gv_mine", "🚚 Chuyến của tôi", "#/log-stop")],
            ["DURING"] = [("bep_batches", "🍳 Mẻ cần làm", "#/prod-batch"), ("attp_checklist", "🧽 Checklist", "#/qc-checklist-result"), ("gv_mine", "🚚 Chuyến của tôi", "#/log-stop"), ("hr_absent", "👥 Ai chưa vào", "#/hr-shift-assign")],
            ["END"] = [("bep_progress", "📈 Tiến độ ca", "#/prod-batch"), ("kh_waste", "♻️ Hao hụt", "#/prod-waste"), ("check_in", "📍 Chấm ra", null)],
        };

        private int LeadMinutes => int.TryParse(configuration["Messaging:ShiftCard:LeadMinutes"], out var m) && m is > 0 and <= 120 ? m : 15;

        /// <summary>Xếp thẻ ca mới tới hạn (idempotent) + trả thẻ đã gửi kèm nội dung mới nhất để messaging sửa tin.</summary>
        public async Task<IResults<ShiftCardModel>> Sync()
        {
            // Gọi từ messaging (khóa client, không có người dùng) → ghi outbox dưới tài khoản hệ thống (như BotChannelService).
            if (!Guid.TryParse(configuration[$"{AppDelicare.Model.Option.SystemUsersOptions.Section}:{nameof(AppDelicare.Model.Option.SystemUsersOptions.JobUserId)}"], out Guid systemUser) || systemUser == Guid.Empty)
            {
                Log.Warning("[ShiftCard] chưa cấu hình SystemUsers:JobUserId – không gửi Thẻ ca");
                return Results<ShiftCardModel>.Success([], ConstantHelper.Message.GetAllSuccessfully);
            }
            using var actorScope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            DateTime now = clock.Now;
            var db = unitOfWork.DbContext;
            var rows = await db.ShiftAssignment.AsNoTracking()
                .Where(a => !a.IsDeleted && a.WorkDate >= now.Date.AddDays(-1) && a.WorkDate <= now.Date && Open.Contains(a.AssignmentStatus!)
                            && a.PlannedStart != null && a.PlannedEnd != null && a.PlannedStart <= now.AddMinutes(LeadMinutes) && a.PlannedEnd!.Value.AddMinutes(30) >= now)
                .ToListAsync();
            var result = new List<ShiftCardModel>();
            foreach (var a in rows)
            {
                try
                {
                    var card = await BuildAsync(a, now);
                    if (card == null)
                    {
                        continue;
                    }
                    await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                    {
                        Template = NotificationCatalog.ShiftCard, EmployeeIds = [a.EmployeeId], Data = card.Value.Data, Buttons = card.Value.Buttons,
                        IdempotencyKey = $"{NotificationCatalog.ShiftCard}:{a.Id:N}", CorrelationId = Correlation + a.Id.ToString("N"),
                    });
                    result.AddRange(await SentCardsAsync(a.Id, card.Value.Data, card.Value.Buttons));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ShiftCard] ca {Assignment}", a.Id);
                }
            }
            return Results<ShiftCardModel>.Success(result, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>⚡ Ca của tôi: thẻ ca hiện tại (hoặc ca kế tiếp / gần nhất hôm nay) của người dùng bot; kèm tin thẻ đã ghim (nếu có) để sửa thay vì gửi mới.</summary>
        public async Task<IResult<ShiftCardModel>> Mine(BotIdentityRequest request)
        {
            var db = unitOfWork.DbContext;
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            if (!bots.TryGetValue(request.BotCode ?? string.Empty, out Guid channel))
            {
                return Result<ShiftCardModel>.Fail(string.Format(ConstantHelper.Message.BotUnknown, request.BotCode));
            }
            var identity = await db.UserExternalIdentity.AsNoTracking().FirstOrDefaultAsync(i => !i.IsDeleted && i.IntegrationChannelId == channel
                && i.ExternalUserId == request.ExternalUserId && (i.IdentityStatus == MessagingCode.Identity.Active || i.IdentityStatus == MessagingCode.Identity.PendingCode));
            Guid? employeeId = identity?.EmployeeId ?? (identity?.UserId is { } uid ? (await HrRules.ResolveEmployeeAsync(db, uid))?.Id : null);
            if (identity == null || employeeId == null)
            {
                return Result<ShiftCardModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
            }
            DateTime now = clock.Now;
            var today = await db.ShiftAssignment.AsNoTracking()
                .Where(a => !a.IsDeleted && a.EmployeeId == employeeId && a.WorkDate >= now.Date.AddDays(-1) && a.WorkDate <= now.Date && a.PlannedStart != null && a.PlannedEnd != null
                            && a.AssignmentStatus != ShiftAssignmentStatus.Absent)
                .ToListAsync();
            var a = today.Where(x => x.PlannedStart!.Value.AddMinutes(-LeadMinutes) <= now && x.PlannedEnd!.Value.AddMinutes(30) >= now).OrderBy(x => x.PlannedStart).FirstOrDefault()
                    ?? today.Where(x => x.PlannedStart > now && x.WorkDate == now.Date).OrderBy(x => x.PlannedStart).FirstOrDefault()
                    ?? today.Where(x => x.WorkDate == now.Date).OrderByDescending(x => x.PlannedStart).FirstOrDefault();
            if (a == null || await BuildAsync(a, now) is not { } card)
            {
                return Result<ShiftCardModel>.Fail(ConstantHelper.Message.BotNoShiftToday);
            }
            var sent = (await SentCardsAsync(a.Id, card.Data, card.Buttons)).FirstOrDefault(c => c.ChatId == identity.ExternalUserId);
            return Result<ShiftCardModel>.Success(sent ?? new ShiftCardModel
            {
                AssignmentId = a.Id, BotCode = request.BotCode, ChatId = identity.ExternalUserId, PayloadJson = Payload(card.Data, card.Buttons),
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        private async Task<List<ShiftCardModel>> SentCardsAsync(Guid assignmentId, object data, List<NotificationButton> buttons)
        {
            var db = unitOfWork.DbContext;
            string corr = Correlation + assignmentId.ToString("N");
            var botOf = (await RecipientResolver.ActiveBotsAsync(db)).ToDictionary(kv => kv.Value, kv => kv.Key);
            var sent = await db.OutboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && m.CorrelationId == corr && m.MessageStatus == MessagingCode.Outbound.Sent && m.ProviderMessageId != null)
                .ToListAsync();
            string payload = Payload(data, buttons);
            return [.. sent.Where(m => botOf.ContainsKey(m.IntegrationChannelId)).Select(m => new ShiftCardModel
            {
                AssignmentId = assignmentId, BotCode = botOf[m.IntegrationChannelId], ChatId = m.ExternalConversationId ?? m.RecipientExternalId,
                ProviderMessageId = m.ProviderMessageId, PayloadJson = payload,
            })];
        }

        private static string Payload(object data, List<NotificationButton> buttons)
            => JsonSerializer.Serialize(new { template = NotificationCatalog.ShiftCard, data, buttons, scope = MessagingCode.Scope.Personal, level = MessagingCode.Level.Normal }, NotificationService.Json);

        /// <summary>Nội dung thẻ: ca, giai đoạn, chấm công, chuyến (tài xế); nút theo giai đoạn ∩ quyền menu của tài khoản (người chỉ có EmployeeId: nút không cần quyền).</summary>
        private async Task<(object Data, List<NotificationButton> Buttons)?> BuildAsync(ShiftAssignment a, DateTime now)
        {
            var db = unitOfWork.DbContext;
            var employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == a.EmployeeId && !e.IsDeleted);
            if (employee == null)
            {
                return null;
            }
            var shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == a.ShiftId);
            DateTime start = a.PlannedStart!.Value, end = a.PlannedEnd!.Value;
            string phase = now < start ? "BEFORE" : now <= end.AddMinutes(-30) ? "DURING" : "END";
            bool checkedIn = a.AssignmentStatus is ShiftAssignmentStatus.CheckedIn or ShiftAssignmentStatus.Completed;
            var lines = new List<string>
            {
                $"🕐 {start:dd/MM} {start:HH:mm}–{end:HH:mm} · {ShiftAssignmentStatus.Label(a.AssignmentStatus)}",
                checkedIn ? "✅ Đã vào ca" : phase == "BEFORE" ? $"⏳ Vào ca lúc {start:HH:mm} – bấm 📍 Chấm công khi tới nơi" : "⚠️ Chưa chấm vào ca",
            };
            var routes = await db.DeliveryRoute.AsNoTracking()
                .Where(r => !r.IsDeleted && r.DriverId == employee.Id && r.DeliveryDate >= a.WorkDate.Date && r.DeliveryDate < a.WorkDate.Date.AddDays(1))
                .OrderBy(r => r.PlannedStart).Take(3).ToListAsync();
            lines.AddRange(routes.Select(r => $"🚚 {r.RouteNo ?? r.Code} · xuất phát {r.PlannedStart:HH:mm} · {DeliveryRouteStatus.Label(r.Status)}"));
            lines.Add(phase switch { "BEFORE" => "Giai đoạn: trước ca", "DURING" => "Giai đoạn: trong ca", _ => "Giai đoạn: cuối ca" });

            // Quyền menu: tài khoản web của nhân viên (định danh có UserId).
            var userIds = await db.UserExternalIdentity.AsNoTracking()
                .Where(i => !i.IsDeleted && i.EmployeeId == employee.Id && i.UserId != null).Select(i => i.UserId!.Value).Distinct().ToListAsync();
            var menus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool admin = false;
            foreach (Guid uid in userIds)
            {
                var perms = await permissionService.GetAsync(uid);
                admin |= perms.IsAdmin;
                menus.UnionWith(perms.Menus.Where(m => m.Value.Access).Select(m => m.Key));
            }
            var buttons = PhaseButtons[phase].Where(b => b.Menu == null || admin || menus.Contains(b.Menu))
                .Where(b => b.Code != "gv_mine" || routes.Count > 0)
                .Select(b => new NotificationButton(b.Label, "sc:run:" + b.Code)).ToList();
            buttons.Add(new NotificationButton("🔄 Làm mới", "sc:refresh"));
            var data = new { title = $"🗓 Thẻ ca – {shift?.Name ?? shift?.Code ?? "Ca"} · {employee.FullName ?? employee.Code}", lines, phase, assignmentId = a.Id };
            return (data, buttons);
        }
    }
}
