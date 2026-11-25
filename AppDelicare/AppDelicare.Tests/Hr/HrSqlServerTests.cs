using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Hr
{
    /// <summary>
    /// BE2.6 – trên SQL Server dev thật (bỏ qua nếu không có DB), trong 1 transaction ngoài rồi ROLLBACK: UNIQUE (EmployeeId, ShiftId, WorkDate) tính cả dòng đã xóa mềm
    /// (đổi ca rồi đổi lại → dùng lại dòng, không vi phạm UNIQUE), phân ca hàng loạt, chấm công GPS theo bán kính trạm bếp (ghi hộ, hình thức GPS), đơn nghỉ duyệt qua engine
    /// (bỏ ca), nhường ca cho người đã có dòng xóa mềm cùng ca / ngày (dùng lại dòng), bảng công. Dữ liệu thử gắn nhãn [DEMO].
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class HrSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Assign_attendance_leave_and_swap_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int assignmentBefore = await db.ShiftAssignment.CountAsync();
            int eventBefore = await db.AttendanceEvent.CountAsync();
            int leaveBefore = await db.LeaveRequest.CountAsync();
            int swapBefore = await db.ShiftSwapRequest.CountAsync();
            string tag = "[DEMO]-B26-" + Guid.NewGuid().ToString("N")[..6];
            DateTime now = clock.Now;
            DateTime workDay = now.AddMinutes(-10).Date;
            DateTime future = clock.Today.AddDays(30);

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid trua = Guid.NewGuid(), toi = Guid.NewGuid(), e1 = Guid.NewGuid(), e2 = Guid.NewGuid(), e3 = Guid.NewGuid();
                db.Shift.AddRange(
                    new Shift { Id = trua, Code = tag + "-TRUA", Name = "[DEMO] Trưa B26", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" },
                    new Shift { Id = toi, Code = tag + "-TOI", Name = "[DEMO] Tối B26", StartTime = new TimeSpan(16, 30, 0), EndTime = new TimeSpan(19, 30, 0), Status = "Active" });
                db.Employee.AddRange(
                    new Employee { Id = e1, Code = tag + "-1", EmployeeCode = tag + "-1", FullName = "[DEMO] NV 1 B26", Status = "Active" },
                    new Employee { Id = e2, Code = tag + "-2", EmployeeCode = tag + "-2", FullName = "[DEMO] NV 2 B26", Status = "Active" },
                    new Employee { Id = e3, Code = tag + "-3", EmployeeCode = tag + "-3", FullName = "[DEMO] NV 3 B26", Status = "Active" });
                // Tọa độ riêng của test (không trùng trạm thật) – bán kính 80 m.
                db.KitchenWorkstation.Add(new KitchenWorkstation { Id = Guid.NewGuid(), Code = tag[..Math.Min(tag.Length, 50)], Name = "[DEMO] Trạm GPS B26", Latitude = -45.1234567m, Longitude = -120.7654321m, RadiusMeters = 80, Status = "Active" });
                await db.SaveChangesAsync("Insert");

                var shifts = scope.ServiceProvider.GetRequiredService<IShiftAssignmentService>();
                // 1) Đổi ca rồi đổi lại trên SQL Server: dòng TRUA (đã xóa mềm) được dùng lại – không vi phạm UQ_ShiftAssignment_Employee_Shift_Date.
                (await shifts.Assign(new ShiftAssignRequest { EmployeeId = e1, WorkDate = future, ShiftId = trua })).ResponseCode.Should().Be(200);
                (await shifts.Assign(new ShiftAssignRequest { EmployeeId = e1, WorkDate = future, ShiftId = toi })).ResponseCode.Should().Be(200);
                var back = await shifts.Assign(new ShiftAssignRequest { EmployeeId = e1, WorkDate = future, ShiftId = trua });
                back.ResponseCode.Should().Be(200, back.ResponseMessage);
                (await db.ShiftAssignment.AsNoTracking().CountAsync(a => a.EmployeeId == e1 && a.WorkDate == future)).Should().Be(2, "TRUA (dùng lại) + TOI (xóa mềm)");
                var bulk = await shifts.BulkAssign(new ShiftBulkAssignRequest { EmployeeIds = [e1, e2], ShiftId = trua, FromDate = future, ToDate = future.AddDays(2) });
                bulk.ResponseCode.Should().Be(200, bulk.ResponseMessage);
                bulk.Data!.Created.Should().Be(5);
                bulk.Data.Skipped.Should().Be(1);

                // 2) Chấm công GPS (ghi hộ bởi admin, hình thức GPS): ca hôm nay từ 10 phút trước; cách trạm ~ 45 m → hợp lệ.
                var today = await shifts.Assign(new ShiftAssignRequest { EmployeeId = e3, WorkDate = workDay, ShiftId = trua, PlannedStart = now.AddMinutes(-10), PlannedEnd = now.AddHours(2) });
                today.ResponseCode.Should().Be(200, today.ResponseMessage);
                var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceEventService>();
                var checkIn = await attendance.CheckIn(new AttendanceCheckRequest { EmployeeId = e3, SourceChannel = "GPS", Latitude = -45.1230567m, Longitude = -120.7654321m, ExternalEventId = tag });
                checkIn.ResponseCode.Should().Be(200, checkIn.ResponseMessage);
                checkIn.Data!.IsValid.Should().BeTrue();
                checkIn.Data.DistanceMeters.Should().BeInRange(40m, 50m);
                checkIn.Data.LateMinutes.Should().Be(10);
                var ev = await db.AttendanceEvent.AsNoTracking().SingleAsync(e => e.ExternalEventId == tag);
                ev.SourceChannel.Should().Be("GPS");
                ev.Note.Should().Contain("Ghi bởi admin").And.Contain("Đi muộn 10 phút");
                ev.Latitude.Should().Be(-45.1230567m);
                (await db.AuditChange.AsNoTracking().Where(a => a.IdChange == ev.Id).Select(a => a.Changes).SingleAsync()).Should().NotContain("45.123");
                var outside = await attendance.CheckOut(new AttendanceCheckRequest { EmployeeId = e3, SourceChannel = "GPS", Latitude = -45.1300000m, Longitude = -120.7654321m });
                outside.ResponseCode.Should().Be(200, outside.ResponseMessage);
                outside.Data!.IsValid.Should().BeFalse("cách ~ 730 m, bán kính 80 m");
                var sheet = await attendance.GetTimesheet(new TimesheetRequest { FromDate = workDay, ToDate = workDay, EmployeeId = e3 });
                sheet.ListData.Single().Present.Should().Be(1);
                sheet.ListData.Single().InvalidEvents.Should().Be(1);

                // 3) Đơn nghỉ (admin lập hộ) → duyệt qua engine → ca trong khoảng nghỉ bị bỏ.
                var leaves = scope.ServiceProvider.GetRequiredService<ILeaveRequestService>();
                var leave = await leaves.Create(new LeaveCreateRequest { EmployeeId = e2, FromDate = future.AddDays(1), ToDate = future.AddDays(1), LeaveTypeCode = "ANNUAL", Reason = "[DEMO] nghỉ phép" });
                leave.ResponseCode.Should().Be(200, leave.ResponseMessage);
                leave.Data!.Request!.RequestNo.Should().MatchRegex(@"^DN-\d{6}-\d{4}$");
                var approve = await leaves.Decide(new HrDecisionRequest { Id = leave.Data.Request.Id, Note = "[DEMO]" }, true);
                approve.ResponseCode.Should().Be(200, approve.ResponseMessage);
                db.ChangeTracker.Clear();
                (await db.LeaveRequest.AsNoTracking().SingleAsync(l => l.Id == leave.Data.Request.Id)).RequestStatus.Should().Be("Approved");
                (await db.ShiftAssignment.AsNoTracking().SingleAsync(a => a.EmployeeId == e2 && a.WorkDate == future.AddDays(1))).IsDeleted.Should().BeTrue();

                // 4) Nhường ca TRUA (future + 2) của NV 1 cho NV 3, NV 3 đã có dòng TRUA cùng ngày đã xóa mềm → engine dùng lại dòng đó.
                (await shifts.Assign(new ShiftAssignRequest { EmployeeId = e3, WorkDate = future.AddDays(2), ShiftId = trua })).ResponseCode.Should().Be(200);
                (await shifts.Assign(new ShiftAssignRequest { EmployeeId = e3, WorkDate = future.AddDays(2), ShiftId = null })).ResponseCode.Should().Be(200);
                Guid give = await db.ShiftAssignment.AsNoTracking().Where(a => a.EmployeeId == e1 && a.WorkDate == future.AddDays(2) && !a.IsDeleted).Select(a => a.Id).SingleAsync();
                var swaps = scope.ServiceProvider.GetRequiredService<IShiftSwapRequestService>();
                var swap = await swaps.Create(new ShiftSwapCreateRequest { CurrentShiftAssignmentId = give, TargetEmployeeId = e3, Reason = "[DEMO] nhường ca" });
                swap.ResponseCode.Should().Be(200, swap.ResponseMessage);
                // UI-6.39 (D-120): người nhận đồng ý trước khi duyệt.
                (await swaps.ConsentForEmployee(e3, new AppDelicare.Model.Model.ShiftSwapConsentRequest { Id = swap.Data!.Request!.Id, Accept = true })).ResponseCode.Should().Be(200);
                var swapApproved = await swaps.Decide(new HrDecisionRequest { Id = swap.Data!.Request!.Id, Note = "[DEMO]" }, true);
                swapApproved.ResponseCode.Should().Be(200, swapApproved.ResponseMessage);
                db.ChangeTracker.Clear();
                var e3Rows = await db.ShiftAssignment.AsNoTracking().Where(a => a.EmployeeId == e3 && a.WorkDate == future.AddDays(2)).ToListAsync();
                e3Rows.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
                (await db.ShiftAssignment.AsNoTracking().SingleAsync(a => a.Id == give)).IsDeleted.Should().BeTrue();
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.ShiftAssignment.CountAsync()).Should().Be(assignmentBefore, "rollback không để lại dữ liệu");
            (await db.AttendanceEvent.CountAsync()).Should().Be(eventBefore);
            (await db.LeaveRequest.CountAsync()).Should().Be(leaveBefore);
            (await db.ShiftSwapRequest.CountAsync()).Should().Be(swapBefore);
        }
    }
}
