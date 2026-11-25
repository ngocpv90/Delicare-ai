#nullable enable
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.6 (T21 – nhân sự): quy tắc dùng chung của phân ca / chấm công / nghỉ / đổi ca (như <c>ProductionRules</c>, <c>ReservationRules</c>) – không bảng, không cột mới.
    /// </summary>
    public static class HrRules
    {
        /// <summary>Giờ bắt đầu / kết thúc kế hoạch của ca trong ngày làm việc; ca qua đêm (kết thúc ≤ bắt đầu, vd. DEM_23H 23:00–01:00) kết thúc ngày hôm sau.</summary>
        public static (DateTime Start, DateTime End) PlannedWindow(DateTime workDate, Shift shift)
        {
            DateTime day = workDate.Date;
            TimeSpan start = shift.StartTime ?? TimeSpan.Zero;
            TimeSpan end = shift.EndTime ?? start;
            DateTime s = day.Add(start);
            DateTime e = day.Add(end);
            if (e <= s)
            {
                e = e.AddDays(1);
            }
            return (s, e);
        }

        /// <summary>Khoảng cách (m) giữa 2 tọa độ – công thức haversine, bán kính Trái Đất 6 371 km.</summary>
        public static decimal DistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        {
            const double R = 6_371_000d;
            double p1 = (double)lat1 * Math.PI / 180d, p2 = (double)lat2 * Math.PI / 180d;
            double dp = p2 - p1, dl = ((double)lon2 - (double)lon1) * Math.PI / 180d;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round((decimal)(R * c), 2);
        }

        private static string Digits(string? phone) => new((phone ?? string.Empty).Where(char.IsDigit).ToArray());

        /// <summary>
        /// HR-ACC-1.3 (D-82 i – thay D-55 b / GAP-HR-02): nhân viên của tài khoản = <see cref="Identity.PersonLink.EmployeeOfUserAsync"/>
        /// (User.PersonId → Employee.PersonId; dự phòng đoán cũ theo cờ BusinessRules:Hr:LegacyEmployeeMatch, có ghi cảnh báo). Giữ tên cũ để mọi nơi gọi dùng chung.
        /// </summary>
        public static Task<Employee?> ResolveEmployeeAsync(DatabaseContext db, Guid userId) => Identity.PersonLink.EmployeeOfUserAsync(db, userId);

        /// <summary>Đơn vị và mọi đơn vị con (theo ParentId); null = không lọc.</summary>
        public static async Task<HashSet<Guid>?> OrganizationScopeAsync(DatabaseContext db, Guid? organizationUnitId)
        {
            if (organizationUnitId is not { } root || root == Guid.Empty)
            {
                return null;
            }
            var all = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).Select(o => new { o.Id, o.ParentId }).ToListAsync();
            var scope = new HashSet<Guid> { root };
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var o in all.Where(o => o.ParentId is { } p && scope.Contains(p) && !scope.Contains(o.Id)))
                {
                    scope.Add(o.Id);
                    added = true;
                }
            }
            return scope;
        }

        /// <summary>Đơn nghỉ Chờ duyệt / Đã duyệt của các nhân viên giao với khoảng ngày.</summary>
        public static Task<List<LeaveRequest>> LeavesAsync(DatabaseContext db, IReadOnlyCollection<Guid> employeeIds, DateTime from, DateTime to, bool approvedOnly)
        {
            DateTime f = from.Date, t = to.Date;
            var statuses = approvedOnly ? new[] { LeaveRequestStatus.Approved } : new[] { LeaveRequestStatus.Pending, LeaveRequestStatus.Approved };
            return db.LeaveRequest.AsNoTracking()
                .Where(l => !l.IsDeleted && employeeIds.Contains(l.EmployeeId) && statuses.Contains(l.RequestStatus) && l.FromDate <= t && l.ToDate >= f)
                .ToListAsync();
        }

        /// <summary>Ca đang nằm trong đơn đổi ca Chờ duyệt.</summary>
        public static async Task<HashSet<Guid>> PendingSwapAssignmentsAsync(DatabaseContext db, IReadOnlyCollection<Guid> assignmentIds)
        {
            var rows = await db.ShiftSwapRequest.AsNoTracking()
                .Where(s => !s.IsDeleted && (s.RequestStatus == ShiftSwapStatus.Pending || s.RequestStatus == ShiftSwapStatus.AwaitingConsent)
                    && (assignmentIds.Contains(s.CurrentShiftAssignmentId) || (s.ProposedShiftAssignmentId != null && assignmentIds.Contains(s.ProposedShiftAssignmentId.Value))))
                .Select(s => new { s.CurrentShiftAssignmentId, s.ProposedShiftAssignmentId }).ToListAsync();
            var set = new HashSet<Guid>();
            foreach (var r in rows)
            {
                set.Add(r.CurrentShiftAssignmentId);
                if (r.ProposedShiftAssignmentId is { } p)
                {
                    set.Add(p);
                }
            }
            return set;
        }

        /// <summary>Ca đã có chấm công (AttendanceEvent).</summary>
        public static async Task<HashSet<Guid>> AttendedAssignmentsAsync(DatabaseContext db, IReadOnlyCollection<Guid> assignmentIds)
            => (await db.AttendanceEvent.AsNoTracking().Where(e => !e.IsDeleted && e.ShiftAssignmentId != null && assignmentIds.Contains(e.ShiftAssignmentId.Value))
                .Select(e => e.ShiftAssignmentId!.Value).Distinct().ToListAsync()).ToHashSet();

        /// <summary>Dựng <see cref="HrAssignmentModel"/> (tên nhân viên, ca, đã chấm công) cho danh sách ca.</summary>
        public static async Task<List<HrAssignmentModel>> ModelsAsync(DatabaseContext db, IReadOnlyCollection<ShiftAssignment> rows)
        {
            if (rows.Count == 0)
            {
                return [];
            }
            var empIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
            var shiftIds = rows.Select(r => r.ShiftId).Distinct().ToList();
            var ids = rows.Select(r => r.Id).ToList();
            var emps = await db.Employee.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
            var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            var attended = await AttendedAssignmentsAsync(db, ids);
            return [.. rows.Select(r => new HrAssignmentModel
            {
                Id = r.Id, EmployeeId = r.EmployeeId, EmployeeCode = emps.GetValueOrDefault(r.EmployeeId)?.Code ?? emps.GetValueOrDefault(r.EmployeeId)?.EmployeeCode,
                EmployeeName = emps.GetValueOrDefault(r.EmployeeId)?.FullName, WorkDate = r.WorkDate, ShiftId = r.ShiftId,
                ShiftCode = shifts.GetValueOrDefault(r.ShiftId)?.Code, ShiftName = shifts.GetValueOrDefault(r.ShiftId)?.Name,
                PlannedStart = r.PlannedStart, PlannedEnd = r.PlannedEnd, Status = r.AssignmentStatus, StatusName = ShiftAssignmentStatus.Label(r.AssignmentStatus),
                IsDeleted = r.IsDeleted, HasAttendance = attended.Contains(r.Id), Note = r.Note,
            }).OrderBy(m => m.WorkDate).ThenBy(m => m.PlannedStart)];
        }

        /// <summary>Ghép ghi chú (giữ ghi chú cũ, cắt 1000 ký tự – độ dài cột Note).</summary>
        public static string? AppendNote(string? note, string? add, int max = 1000)
        {
            if (string.IsNullOrWhiteSpace(add))
            {
                return note;
            }
            string merged = string.IsNullOrWhiteSpace(note) ? add.Trim() : note.Trim() + " | " + add.Trim();
            return merged.Length <= max ? merged : merged[..max];
        }

        public static string Day(DateTime d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}
