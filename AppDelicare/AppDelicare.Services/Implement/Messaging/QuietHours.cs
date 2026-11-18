using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Services.Jobs;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Kế hoạch gửi theo giờ yên lặng: người nhận → thời điểm được gửi (null = gửi ngay); <see cref="Skip"/> = không gửi (🔴 cho người ngoài ca).</summary>
    public sealed record QuietPlan(Dictionary<Guid, DateTime?> NotBefore, HashSet<Guid> Skip)
    {
        public static QuietPlan None => new([], []);

        public DateTime? For(Guid identityId) => NotBefore.GetValueOrDefault(identityId);
    }

    /// <summary>
    /// BOT-2.3 (BOT_TELEGRAM §5.3 – PROPOSED, cấu hình <c>Messaging:QuietHours</c>): giờ yên lặng theo phân ca.
    /// <list type="bullet">
    /// <item>🟡 / ⚪: người đang trong ca hoặc trong <c>LeadMinutes</c> trước ca → gửi ngay; ngoài ca → hẹn tới <c>LeadMinutes</c> trước ca kế tiếp
    /// (trong <c>LookaheadDays</c> ngày). Người không có phân ca (văn phòng, quản lý): gửi trong [DayStart, DayEnd), ngoài khung → hẹn DayStart kế tiếp.</item>
    /// <item>🔴: gửi ngay cho người đang trong ca / không có phân ca; người có phân ca nhưng ngoài ca → bỏ qua. Nếu bỏ hết (không ai đang trực) → vẫn gửi
    /// tất cả (không để mất tin khẩn – leo thang BOT-2.2 dựa vào tin đã gửi).</item>
    /// <item>Tin leo thang (<c>NotificationRequest.Escalation</c>) → gửi ngay, không xét ca.</item>
    /// </list>
    /// Người ↔ nhân viên: EmployeeId của định danh bot, không có thì theo tài khoản (<see cref="HrRules.ResolveEmployeeAsync"/>). Ca = ShiftAssignment (PlannedStart / PlannedEnd), bỏ ca Vắng.
    /// </summary>
    public static class QuietHours
    {
        private static readonly string[] Counted = [ShiftAssignmentStatus.Planned, ShiftAssignmentStatus.Confirmed, ShiftAssignmentStatus.CheckedIn, ShiftAssignmentStatus.Completed];

        public static async Task<QuietPlan> PlanAsync(DatabaseContext db, IReadOnlyList<ResolvedRecipient> recipients, string level, DateTime now,
            QuietHoursOptions options, bool escalation, CancellationToken ct = default)
        {
            if (!options.Enabled || escalation || recipients.Count == 0)
            {
                return QuietPlan.None;
            }
            // Người → nhân viên.
            var employeeOf = new Dictionary<Guid, Guid?>();
            var byUser = new Dictionary<Guid, Guid?>();
            foreach (var r in recipients)
            {
                Guid? employee = r.EmployeeId;
                if (employee == null && r.UserId is { } uid)
                {
                    if (!byUser.TryGetValue(uid, out employee))
                    {
                        employee = (await HrRules.ResolveEmployeeAsync(db, uid))?.Id;
                        byUser[uid] = employee;
                    }
                }
                employeeOf[r.IdentityId] = employee;
            }
            var employees = employeeOf.Values.Where(e => e != null).Select(e => e!.Value).Distinct().ToList();
            int lead = Math.Max(0, options.LeadMinutes);
            DateTime from = now.Date.AddDays(-1), to = now.Date.AddDays(Math.Max(1, options.LookaheadDays) + 1);
            var shifts = employees.Count == 0 ? [] : await db.ShiftAssignment.AsNoTracking()
                .Where(a => !a.IsDeleted && employees.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate < to
                            && a.PlannedStart != null && a.PlannedEnd != null && Counted.Contains(a.AssignmentStatus))
                .Select(a => new { a.EmployeeId, Start = a.PlannedStart!.Value, End = a.PlannedEnd!.Value })
                .ToListAsync(ct);
            var shiftsOf = shifts.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.Select(s => (s.Start, s.End)).ToList());

            var plan = new QuietPlan([], []);
            bool urgent = level == MessagingCode.Level.Urgent;
            foreach (var r in recipients)
            {
                var mine = employeeOf[r.IdentityId] is { } e && shiftsOf.TryGetValue(e, out var list) ? list : null;
                DateTime? next = mine?.Where(s => s.Start.AddMinutes(-lead) > now).Select(s => (DateTime?)s.Start.AddMinutes(-lead)).Min();
                bool onDuty = mine?.Any(s => s.Start.AddMinutes(-lead) <= now && now <= s.End) == true;
                if (onDuty)
                {
                    continue;
                }
                if (next != null)
                {
                    // Có phân ca nhưng đang ngoài ca.
                    if (urgent)
                    {
                        plan.Skip.Add(r.IdentityId);
                    }
                    else
                    {
                        plan.NotBefore[r.IdentityId] = next;
                    }
                    continue;
                }
                // Không có phân ca (hoặc hết ca trong khoảng tìm) → khung giờ hành chính.
                if (!urgent && DayWindow(now, options) is { } wake)
                {
                    plan.NotBefore[r.IdentityId] = wake;
                }
            }
            if (urgent && plan.Skip.Count == recipients.Count)
            {
                plan.Skip.Clear();
            }
            return plan;
        }

        /// <summary>Ngoài [DayStart, DayEnd) → DayStart kế tiếp; trong khung → null.</summary>
        public static DateTime? DayWindow(DateTime now, QuietHoursOptions options)
        {
            TimeSpan start = JobSchedule.ParseAt(options.DayStart, "07:00"), end = JobSchedule.ParseAt(options.DayEnd, "21:00");
            TimeSpan t = now.TimeOfDay;
            if (t >= start && t < end)
            {
                return null;
            }
            return t < start ? now.Date + start : now.Date.AddDays(1) + start;
        }
    }
}
