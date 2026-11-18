using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Một người nhận đã liên kết bot: định danh + kênh (bot) sẽ gửi.</summary>
    public sealed record ResolvedRecipient(Guid IdentityId, Guid ChannelId, string BotCode, string ExternalUserId, Guid? UserId, Guid? EmployeeId);

    /// <summary>
    /// BOT-1.1 (D-62): tìm người nhận cho outbox. Mỗi người (UserId hoặc EmployeeId) → định danh <c>Active</c> trên bot đích; người không có bot đích
    /// → bot Telegram khác người đó đang có (BOT_TELEGRAM §0 "người không có bot đích → bot người đó có"); không có bot nào → bỏ qua (không lỗi).
    /// Người nhận theo quyền (UserGroup × ObjectMenu) dùng <see cref="UsersWithMenuAsync"/>; theo ca / nhóm chat làm ở BOT-1.7 / 2.1.
    /// </summary>
    public static class RecipientResolver
    {
        /// <summary>Bot đang hoạt động: BotCode → IntegrationChannelId (BotConfiguration.BotStatus + IntegrationChannel.ChannelStatus = Active).</summary>
        public static async Task<Dictionary<string, Guid>> ActiveBotsAsync(DatabaseContext db, CancellationToken ct = default)
        {
            var rows = await (from b in db.BotConfiguration.AsNoTracking()
                              join c in db.IntegrationChannel.AsNoTracking() on b.IntegrationChannelId equals c.Id
                              where !b.IsDeleted && !c.IsDeleted && b.BotStatus == MessagingCode.StatusActive && c.ChannelStatus == MessagingCode.StatusActive
                                    && c.ProviderCode == MessagingCode.ProviderTelegram
                              select new { b.BotCode, c.Id })
                             .ToListAsync(ct);
            return rows.GroupBy(r => r.BotCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<List<ResolvedRecipient>> ResolveAsync(DatabaseContext db, string botCode, IReadOnlyCollection<Guid> userIds,
            IReadOnlyCollection<Guid> employeeIds, CancellationToken ct = default)
        {
            var bots = await ActiveBotsAsync(db, ct);
            if (bots.Count == 0 || (userIds.Count == 0 && employeeIds.Count == 0))
            {
                return [];
            }
            var channelIds = bots.Values.ToList();
            var botOfChannel = bots.ToDictionary(kv => kv.Value, kv => kv.Key);
            var identities = await db.UserExternalIdentity.AsNoTracking()
                .Where(i => !i.IsDeleted && i.IdentityStatus == MessagingCode.Identity.Active && channelIds.Contains(i.IntegrationChannelId)
                            && ((i.UserId != null && userIds.Contains(i.UserId.Value)) || (i.EmployeeId != null && employeeIds.Contains(i.EmployeeId.Value))))
                .ToListAsync(ct);
            // Gom theo người: ưu tiên khóa UserId (người có tài khoản web), không có thì EmployeeId.
            var result = new List<ResolvedRecipient>();
            foreach (var person in identities.GroupBy(PersonKey))
            {
                var preferred = person.Where(i => botOfChannel[i.IntegrationChannelId].Equals(botCode, StringComparison.OrdinalIgnoreCase)).ToList();
                // Fallback: bot nhà (VH) trước, rồi bot khác.
                var chosen = preferred.Count > 0 ? preferred
                    : person.OrderBy(i => botOfChannel[i.IntegrationChannelId] == MessagingCode.BotVanHanh ? 0 : 1).Take(1).ToList();
                result.AddRange(chosen.Select(i => new ResolvedRecipient(i.Id, i.IntegrationChannelId, botOfChannel[i.IntegrationChannelId], i.ExternalUserId, i.UserId, i.EmployeeId)));
            }
            return result;
        }

        private static string PersonKey(UserExternalIdentity i) => i.UserId != null ? "U:" + i.UserId : "E:" + i.EmployeeId;

        /// <summary>Tài khoản đang hoạt động có quyền vào (AllowAccess) menu <paramref name="metaLink"/> qua nhóm quyền (UserClaim → UserRole), kèm admin.</summary>
        public static async Task<List<Guid>> UsersWithMenuAsync(DatabaseContext db, string metaLink, CancellationToken ct = default)
        {
            var byGroup = from uc in db.UserClaim.AsNoTracking()
                          join g in db.UserGroup.AsNoTracking() on uc.UserGroupId equals g.Id
                          join r in db.UserRole.AsNoTracking() on g.Id equals r.UserGroupId
                          join m in db.ObjectMenu.AsNoTracking() on r.ObjectMenuId equals m.Id
                          join u in db.User.AsNoTracking() on uc.UserId equals u.Id
                          where !uc.IsDeleted && !g.IsDeleted && !r.IsDeleted && !m.IsDeleted && m.IsActive && r.AllowAccess && m.MetaLink == metaLink
                                && !u.IsDeleted && u.IsActive
                          select u.Id;
            var admins = db.User.AsNoTracking().Where(u => !u.IsDeleted && u.IsActive && u.IsAdmin).Select(u => u.Id);
            return await byGroup.Union(admins).Distinct().ToListAsync(ct);
        }
    }
}
