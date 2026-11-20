using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Hr
{
    /// <summary>
    /// BE2.6 (T21 – nhân sự): lưới ca, phân ca (upsert, dòng đã xóa mềm dùng lại vì UNIQUE), phân ca hàng loạt, xác nhận; chấm công GPS theo bán kính trạm bếp / QR / chấm hộ
    /// (T21 AC: hợp lệ theo bán kính, chấm hộ ghi rõ SourceChannel), chống trùng, đi muộn / về sớm, bảng công, đánh vắng; đơn nghỉ / đổi ca / nhường ca + duyệt qua engine,
    /// hủy. Hôm nay (đồng hồ test) = Thứ 5 10/12/2026.
    /// </summary>
    public class HrTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 10);
        private static readonly DateTime Monday = new(2026, 12, 14);
        private readonly InMemoryApiFactory _factory;

        public HrTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(8);
        }

        private static string Dt(DateTime d) => d.ToString("yyyy-MM-ddTHH:mm:ss");

        /// <summary>User thường (tên đăng nhập = <paramref name="userName"/>) thuộc 1 nhóm có quyền Duyệt trên "#/wf-inbox?type=&lt;screen&gt;" + quyền Thêm / Sửa trên "#/&lt;screen&gt;".</summary>
        private static async Task<Guid> UserAsync(InMemoryApiFactory factory, string userName, string[]? approve = null, string[]? edit = null, Guid? personId = null)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = userName, IsActive = true, PersonId = personId });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "HR-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (var link in (approve ?? []).Select(s => "#/wf-inbox?type=" + s).Concat((edit ?? []).Select(s => "#/" + s)))
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                }
            });
            return userId;
        }

        private sealed record World(Guid Trua, Guid Toi, Guid Dem, Guid ParentOu, Guid ChildOu, Guid[] Employees, string[] Codes);

        /// <summary>3 ca (Trưa 10:30–13:30, Tối 16:30–19:30, Đêm 23:00–01:00), đơn vị cha / con, <paramref name="count"/> nhân viên (cuối cùng ở đơn vị con) + 1 nhân viên nghỉ việc.</summary>
        private async Task<World> WorldAsync(string key, int count = 4)
        {
            Guid trua = Guid.NewGuid(), toi = Guid.NewGuid(), dem = Guid.NewGuid(), parent = Guid.NewGuid(), child = Guid.NewGuid(), company = Guid.NewGuid();
            var ids = Enumerable.Range(0, count + 1).Select(_ => Guid.NewGuid()).ToArray();
            var codes = Enumerable.Range(0, count + 1).Select(i => $"{key}-NV{i + 1}").ToArray();
            await _factory.SeedAsync(db =>
            {
                db.Shift.AddRange(
                    new Shift { Id = trua, Code = key + "-TRUA", Name = "Trưa " + key, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" },
                    new Shift { Id = toi, Code = key + "-TOI", Name = "Tối " + key, StartTime = new TimeSpan(16, 30, 0), EndTime = new TimeSpan(19, 30, 0), Status = "Active" },
                    new Shift { Id = dem, Code = key + "-DEM", Name = "Đêm " + key, StartTime = new TimeSpan(23, 0, 0), EndTime = new TimeSpan(1, 0, 0), Status = "Active" });
                db.OrganizationUnit.AddRange(
                    new OrganizationUnit { Id = parent, CompanyId = company, Code = key + "-BEP", Name = "Bếp " + key, UnitType = "BRANCH" },
                    new OrganizationUnit { Id = child, CompanyId = company, ParentId = parent, Code = key + "-TO", Name = "Tổ nấu " + key, UnitType = "TEAM" });
                for (int i = 0; i <= count; i++)
                {
                    db.Employee.Add(new Employee
                    {
                        Id = ids[i], Code = codes[i], EmployeeCode = codes[i], FullName = $"NV {i + 1} {key}", OrganizationUnitId = i == count - 1 ? child : parent,
                        Status = i == count ? "Inactive" : "Active",
                    });
                }
                return Task.CompletedTask;
            });
            return new World(trua, toi, dem, parent, child, ids, codes);
        }

        private static Task<LegacyResult> Assign(HttpClient c, Guid employee, DateTime day, Guid? shift, params (string, object?)[] extra)
            => c.PutFormAsync("/api/ShiftAssignment/Assign", [("EmployeeId", employee), ("WorkDate", day), ("ShiftId", shift), .. extra]);

        private Task<List<ShiftAssignment>> RowsAsync(Guid employee) => _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().Where(a => a.EmployeeId == employee).ToListAsync());

        [Fact]
        public async Task Grid_assign_bulk_and_confirm_follow_unique_rule_and_lock_rules()
        {
            var w = await WorldAsync("G1");
            using var admin = _factory.CreateAuthorizedClient();
            Guid e1 = w.Employees[0], e2 = w.Employees[1], inactive = w.Employees[4];

            var created = await Assign(admin, e1, Monday, w.Trua);
            var data = created.Ok();
            data.GetProperty("action").GetString().Should().Be("Created");
            data.GetProperty("assignment").GetProperty("plannedStart").GetDateTime().Should().Be(Monday.AddHours(10.5));
            data.GetProperty("assignment").GetProperty("plannedEnd").GetDateTime().Should().Be(Monday.AddHours(13.5));
            data.GetProperty("assignment").GetProperty("status").GetString().Should().Be("Planned");
            created.Message.Should().Contain("G1-TRUA").And.Contain("14/12/2026");

            // Đổi sang ca đêm: dòng TRUA xóa mềm, ca đêm kết thúc 01:00 hôm sau.
            var night = (await Assign(admin, e1, Monday, w.Dem)).Ok();
            night.GetProperty("action").GetString().Should().Be("Updated");
            night.GetProperty("assignment").GetProperty("plannedEnd").GetDateTime().Should().Be(Monday.AddDays(1).AddHours(1));
            // Đổi lại TRUA: UNIQUE (nhân viên, ca, ngày) tính cả dòng đã xóa → dùng lại dòng cũ, không thêm dòng.
            (await Assign(admin, e1, Monday, w.Trua)).Ok();
            var rows = await RowsAsync(e1);
            rows.Count(r => r.ShiftId == w.Trua && r.WorkDate == Monday).Should().Be(1);
            rows.Single(r => r.ShiftId == w.Trua).IsDeleted.Should().BeFalse();
            rows.Single(r => r.ShiftId == w.Dem).IsDeleted.Should().BeTrue();
            rows.Single(r => r.ShiftId == w.Trua).AssignedBy.Should().Be(InMemoryApiFactory.UserId);

            // Giờ kế hoạch riêng (tăng ca) + kiểm hợp lệ.
            (await Assign(admin, e2, Monday, w.Toi, ("PlannedStart", Dt(Monday.AddHours(15))), ("PlannedEnd", Dt(Monday.AddHours(21))))).Ok()
                .GetProperty("assignment").GetProperty("plannedEnd").GetDateTime().Should().Be(Monday.AddHours(21));
            (await Assign(admin, e2, Monday, w.Toi, ("PlannedStart", Dt(Monday.AddHours(21))), ("PlannedEnd", Dt(Monday.AddHours(20))))).Fail("Giờ kế hoạch");
            (await Assign(admin, e1, Today.AddDays(-1), w.Trua)).Fail("ngày đã qua");
            (await Assign(admin, inactive, Monday, w.Trua)).Fail("nghỉ việc");
            (await admin.PostFormAsync("/api/ShiftAssignment/Insert", ("EmployeeId", e1), ("ShiftId", w.Trua), ("WorkDate", Monday))).Fail("lưới ca");

            // Hàng loạt: 3 nhân viên × 14–20/12, bỏ Chủ nhật → e1 / e2 thứ 2 đã có ca → bỏ qua; nhân viên nghỉ việc bỏ qua.
            var bulk = await admin.PostFormAsync("/api/ShiftAssignment/BulkAssign",
                ("EmployeeIds", e1), ("EmployeeIds", e2), ("EmployeeIds", w.Employees[2]), ("EmployeeIds", inactive),
                ("ShiftId", w.Trua), ("FromDate", Monday), ("ToDate", Monday.AddDays(6)), ("SkipWeekdays", 7));
            var b = bulk.Ok();
            b.GetProperty("created").GetInt32().Should().Be(3 * 6 - 2);
            b.GetProperty("skipped").GetInt32().Should().Be(3);
            b.GetProperty("listSkipped").EnumerateArray().Select(x => x.GetProperty("reason").GetString()).Should().Contain(r => r!.Contains("Đã có ca G1-TOI"));
            (await RowsAsync(e1)).Where(r => !r.IsDeleted).Should().HaveCount(6, "thứ 2 đã có + thứ 3 → thứ 7, bỏ Chủ nhật");
            (await admin.PostFormAsync("/api/ShiftAssignment/BulkAssign", ("EmployeeIds", e1), ("ShiftId", w.Trua), ("FromDate", Monday), ("ToDate", Monday.AddDays(40)))).Fail("tối đa 31");
            // Theo đơn vị (gồm tổ con): nhân viên 4 ở tổ con.
            var byOu = (await admin.PostFormAsync("/api/ShiftAssignment/BulkAssign", ("OrganizationUnitId", w.ParentOu), ("ShiftId", w.Toi),
                ("FromDate", Monday.AddDays(7)), ("ToDate", Monday.AddDays(7)))).Ok();
            byOu.GetProperty("created").GetInt32().Should().Be(4);

            // Lưới: 7 ngày từ thứ 2, đơn vị cha gồm cả tổ con, nhân viên nghỉ việc không hiện.
            var grid = (await admin.GetResultAsync($"/api/ShiftAssignment/GetGrid?FromDate=2026-12-14&OrganizationUnitId={w.ParentOu}")).Ok();
            grid.GetProperty("days").EnumerateArray().Select(x => x.GetString()).Should().HaveCount(7).And.StartWith("2026-12-14");
            var emps = grid.GetProperty("employees").EnumerateArray().ToList();
            emps.Should().HaveCount(4);
            var cell = emps.Single(e => e.GetProperty("id").GetGuid() == e1).GetProperty("cells").GetProperty("2026-12-14");
            cell.GetProperty("shiftCode").GetString().Should().Be("G1-TRUA");
            cell.GetProperty("status").GetString().Should().Be("Planned");
            emps.Single(e => e.GetProperty("id").GetGuid() == e1).GetProperty("cells").GetProperty("2026-12-20").GetProperty("assignmentId").ValueKind.Should().Be(JsonValueKind.Null);
            (await admin.GetResultAsync($"/api/ShiftAssignment/GetGrid?FromDate=2026-12-14&OrganizationUnitId={w.ChildOu}")).Ok()
                .GetProperty("employees").EnumerateArray().Should().ContainSingle();

            // Xác nhận thứ 2 của đơn vị: 3 ca Kế hoạch → Xác nhận; chạy lại = 0.
            var confirm = (await admin.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", Monday), ("OrganizationUnitId", w.ParentOu))).Ok();
            confirm.GetProperty("confirmed").GetInt32().Should().Be(3);
            confirm.GetProperty("notificationSent").GetBoolean().Should().BeFalse();
            (await admin.PostFormAsync("/api/ShiftAssignment/Confirm", ("WorkDate", Monday), ("OrganizationUnitId", w.ParentOu))).Ok()
                .GetProperty("alreadyConfirmed").GetInt32().Should().Be(3);
            // Đổi giờ ca đã xác nhận → về Kế hoạch + cảnh báo; bỏ ca.
            var reset = await Assign(admin, e2, Monday, w.Toi, ("PlannedStart", Dt(Monday.AddHours(16))), ("PlannedEnd", Dt(Monday.AddHours(20))));
            reset.Ok().GetProperty("assignment").GetProperty("status").GetString().Should().Be("Planned");
            reset.Data.GetProperty("listWarning").EnumerateArray().Should().ContainSingle();
            (await Assign(admin, e2, Monday, null)).Ok().GetProperty("action").GetString().Should().Be("Removed");
            (await RowsAsync(e2)).Single(r => r.WorkDate == Monday).IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task Attendance_validates_gps_radius_qr_proxy_dedup_and_builds_timesheet()
        {
            var w = await WorldAsync("A1");
            using var admin = _factory.CreateAuthorizedClient();
            Guid e1 = w.Employees[0], e2 = w.Employees[1], e3 = w.Employees[2], e4 = w.Employees[3];
            Guid person2 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.KitchenWorkstation.Add(new KitchenWorkstation { Id = Guid.NewGuid(), Code = "A1-KW", Name = "Bếp trung tâm A1", Latitude = 10.7769000m, Longitude = 106.7009000m, RadiusMeters = 100, Status = "Active" });
                db.Person.Add(new Person { Id = person2, FullName = "NV 2 A1", PhoneNumber = "0901 234 567" });
                db.Employee.Single(e => e.Id == e2).Phone = "+84 0901-234-567".Replace("+84 ", string.Empty);
                return Task.CompletedTask;
            });
            using var u1 = _factory.CreateAuthorizedClient(await UserAsync(_factory, w.Codes[0]));          // nhân viên 1: mã nhân viên = tên đăng nhập
            using var u2 = _factory.CreateAuthorizedClient(await UserAsync(_factory, "a1-sdt", personId: person2)); // nhân viên 2: theo số điện thoại hồ sơ
            using var stranger = _factory.CreateAuthorizedClient(await UserAsync(_factory, "a1-khach"));
            foreach (var e in new[] { e1, e2, e3, e4 })
            {
                (await Assign(admin, e, Today, w.Trua)).Ok();
            }

            // 08:00 – quá sớm (ca 10:30, vào sớm tối đa 120 phút).
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 10.7772m), ("Longitude", 106.7010m))).Fail("vào ca được từ 08:30");
            _factory.Clock.FixedNow = Today.AddHours(10).AddMinutes(20);
            (await stranger.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 10.7772m), ("Longitude", 106.7010m))).Fail("chưa gắn với nhân viên");
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("SourceChannel", "GPS"))).Fail("cần vị trí");
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("SourceChannel", "PROXY"), ("Note", "x"))).Fail("GPS hoặc quét QR");
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("SourceChannel", "FAX"))).Fail("không hợp lệ");
            var in1 = await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 10.7772m), ("Longitude", 106.7010m), ("SourceChannel", "MOBILE"), ("ExternalEventId", "A1-EV-1"));
            var ev1 = in1.Ok();
            ev1.GetProperty("isValid").GetBoolean().Should().BeTrue();
            ev1.GetProperty("sourceChannel").GetString().Should().Be("GPS");
            ev1.GetProperty("distanceMeters").GetDecimal().Should().BeInRange(30m, 40m, "≈ 35 m về phía bắc");
            ev1.GetProperty("workstationCode").GetString().Should().Be("A1-KW");
            ev1.GetProperty("lateMinutes").GetInt32().Should().Be(0);
            ev1.GetProperty("assignment").GetProperty("status").GetString().Should().Be("CheckedIn");
            ev1.TryGetProperty("latitude", out _).Should().BeFalse("không trả lại tọa độ");
            // Gửi lại cùng ExternalEventId → không ghi thêm; vào ca lần 2 → 400.
            var dup = await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 10.7772m), ("Longitude", 106.7010m), ("ExternalEventId", "A1-EV-1"));
            dup.Ok().GetProperty("isDuplicate").GetBoolean().Should().BeTrue();
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 10.7772m), ("Longitude", 106.7010m))).Fail("chưa ra ca");
            (await _factory.QueryAsync(db => db.AttendanceEvent.CountAsync(e => e.EmployeeId == e1))).Should().Be(1);
            // Nhật ký AuditChange không chép tọa độ.
            var audit = await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.EntityName == "AttendanceEvent").Select(a => a.Changes).ToListAsync());
            audit.Should().NotBeEmpty();
            audit.Should().OnlyContain(c => !c!.Contains("10.7772") && !c.Contains("10,7772"));

            // 10:42 – nhân viên 2 (theo số điện thoại) cách ~ 550 m → vẫn ghi, IsValid = false + cảnh báo; đi muộn 12 phút.
            _factory.Clock.FixedNow = Today.AddHours(10).AddMinutes(42);
            var in2 = await u2.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 10.7819m), ("Longitude", 106.7009m));
            var ev2 = in2.Ok();
            ev2.GetProperty("isValid").GetBoolean().Should().BeFalse();
            ev2.GetProperty("lateMinutes").GetInt32().Should().Be(12);
            ev2.GetProperty("listWarning").EnumerateArray().Single().GetString().Should().Contain("Ngoài bán kính").And.Contain("cho phép 100 m");
            in2.Message.Should().Contain("Ngoài bán kính");

            // Chấm hộ: không có quyền → 400; admin phải ghi rõ hình thức, PROXY phải có lý do; QR sai mã trạm → 400.
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("EmployeeId", e3), ("SourceChannel", "PROXY"), ("Note", "Quên điện thoại"))).Fail("Không có quyền");
            (await admin.PostFormAsync("/api/AttendanceEvent/CheckIn", ("EmployeeId", e3))).Fail("ghi rõ hình thức");
            (await admin.PostFormAsync("/api/AttendanceEvent/CheckIn", ("EmployeeId", e3), ("SourceChannel", "PROXY"))).Fail("lý do");
            (await admin.PostFormAsync("/api/AttendanceEvent/CheckIn", ("EmployeeId", e3), ("SourceChannel", "QR"), ("WorkstationCode", "KHONG-CO"))).Fail("Không tìm thấy trạm bếp");
            var proxy = (await admin.PostFormAsync("/api/AttendanceEvent/CheckIn", ("EmployeeId", e3), ("SourceChannel", "PROXY"), ("Note", "Quên điện thoại"))).Ok();
            proxy.GetProperty("sourceChannel").GetString().Should().Be("PROXY");
            proxy.GetProperty("isValid").GetBoolean().Should().BeTrue();
            proxy.GetProperty("note").GetString().Should().Contain("Ghi bởi test-admin").And.Contain("Chấm hộ").And.Contain("Quên điện thoại");

            // 13:00 ra ca: nhân viên 1 về sớm 30 phút → Hoàn thành.
            _factory.Clock.FixedNow = Today.AddHours(13);
            (await stranger.PostFormAsync("/api/AttendanceEvent/CheckOut", ("Latitude", 10.7772m), ("Longitude", 106.7010m))).Fail("chưa gắn");
            var out1 = (await u1.PostFormAsync("/api/AttendanceEvent/CheckOut", ("Latitude", 10.7770m), ("Longitude", 106.7009m))).Ok();
            out1.GetProperty("earlyLeaveMinutes").GetInt32().Should().Be(30);
            out1.GetProperty("assignment").GetProperty("status").GetString().Should().Be("Completed");
            (await u1.PostFormAsync("/api/AttendanceEvent/CheckOut", ("Latitude", 10.7770m), ("Longitude", 106.7009m))).Fail("chưa vào ca");
            var mine = (await u1.GetResultAsync("/api/AttendanceEvent/GetMine")).Ok();
            mine.GetProperty("listAssignment").EnumerateArray().Single().GetProperty("workedMinutes").GetInt32().Should().Be(160);
            (await admin.PostFormAsync("/api/AttendanceEvent/Insert", ("EmployeeId", e1), ("EventType", "CHECK_IN"), ("EventAt", Today))).Fail("vào ca / ra ca");

            // 14:00 – bảng công: nhân viên 4 không vào ca → vắng; nhân viên 2 / 3 thiếu ra ca.
            _factory.Clock.FixedNow = Today.AddHours(14);
            var sheet = (await admin.GetResultAsync($"/api/AttendanceEvent/GetTimesheet?FromDate=2026-12-10&ToDate=2026-12-10&OrganizationUnitId={w.ParentOu}")).ListData.EnumerateArray()
                .ToDictionary(x => x.GetProperty("employeeId").GetGuid());
            sheet.Should().HaveCount(5, "kể cả nhân viên đã nghỉ việc (bảng công quá khứ)");
            sheet[e1].GetProperty("present").GetInt32().Should().Be(1);
            sheet[e1].GetProperty("earlyLeave").GetInt32().Should().Be(1);
            sheet[e1].GetProperty("earlyLeaveMinutes").GetInt32().Should().Be(30);
            sheet[e1].GetProperty("workedMinutes").GetInt32().Should().Be(160);
            sheet[e2].GetProperty("late").GetInt32().Should().Be(1);
            sheet[e2].GetProperty("lateMinutes").GetInt32().Should().Be(12);
            sheet[e2].GetProperty("invalidEvents").GetInt32().Should().Be(1);
            sheet[e2].GetProperty("missingCheckOut").GetInt32().Should().Be(1);
            sheet[e4].GetProperty("absent").GetInt32().Should().Be(1);
            sheet[e4].GetProperty("present").GetInt32().Should().Be(0);
            var detail = (await admin.GetResultAsync($"/api/AttendanceEvent/GetTimesheetDetail?FromDate=2026-12-10&ToDate=2026-12-10&EmployeeId={e2}")).ListData.EnumerateArray().Single();
            detail.GetProperty("checkInValid").GetBoolean().Should().BeFalse();
            detail.GetProperty("checkInDistanceMeters").GetDecimal().Should().BeGreaterThan(500m);
            (await admin.GetResultAsync("/api/AttendanceEvent/GetTimesheet?FromDate=2026-12-10&ToDate=2026-09-10")).Code.Should().Be(400);

            // Đánh vắng: chỉ ca hết giờ không chấm công (nhân viên 4).
            var absent = (await admin.PostFormAsync("/api/ShiftAssignment/MarkAbsent", ("WorkDate", Today), ("OrganizationUnitId", w.ParentOu))).Ok();
            absent.GetProperty("marked").GetInt32().Should().Be(1);
            (await RowsAsync(e4)).Single().AssignmentStatus.Should().Be("Absent");
            // Ca đã chấm công không đổi / bỏ được.
            _factory.Clock.FixedNow = Today.AddHours(8);
            (await Assign(admin, e1, Today, null)).Fail("chấm công");
        }

        [Fact]
        public async Task Outside_radius_can_be_blocked_and_qr_needs_workstation()
        {
            await using var factory = new InMemoryApiFactory();
            factory.Settings["BusinessRules:Hr:RejectOutsideRadius"] = "true";
            factory.Clock.FixedNow = Today.AddHours(10).AddMinutes(30);
            using var admin = factory.CreateAuthorizedClient();
            Guid shift = Guid.NewGuid(), emp = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "R1-TRUA", Name = "Trưa", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.Employee.Add(new Employee { Id = emp, Code = "R1-NV", FullName = "NV R1", Status = "Active" });
                db.KitchenWorkstation.Add(new KitchenWorkstation { Id = Guid.NewGuid(), Code = "R1-KW", Name = "Bếp R1", Latitude = 21.0285m, Longitude = 105.8542m, Status = "Active" });
                return Task.CompletedTask;
            });
            using var user = factory.CreateAuthorizedClient(await UserAsync(factory, "R1-NV"));
            (await Assign(admin, emp, Today, shift)).Ok();
            // Trạm chưa nhập bán kính → mặc định 150 m; cách ~ 330 m → chặn, không ghi.
            (await user.PostFormAsync("/api/AttendanceEvent/CheckIn", ("Latitude", 21.0315m), ("Longitude", 105.8542m))).Fail("cho phép 150 m");
            (await factory.QueryAsync(db => db.AttendanceEvent.CountAsync())).Should().Be(0);
            (await user.PostFormAsync("/api/AttendanceEvent/CheckIn", ("SourceChannel", "QR"))).Fail("WorkstationCode");
            var qr = (await user.PostFormAsync("/api/AttendanceEvent/CheckIn", ("SourceChannel", "QR"), ("WorkstationCode", "r1-kw"))).Ok();
            qr.GetProperty("isValid").GetBoolean().Should().BeTrue();
            qr.GetProperty("workstationCode").GetString().Should().Be("R1-KW");
        }

        [Fact]
        public async Task Leave_request_goes_through_engine_removes_shifts_and_can_be_rejected_or_cancelled()
        {
            var w = await WorldAsync("L1");
            using var admin = _factory.CreateAuthorizedClient();
            Guid e1 = w.Employees[0], e2 = w.Employees[1];
            using var staff = _factory.CreateAuthorizedClient(await UserAsync(_factory, w.Codes[0]));
            using var other = _factory.CreateAuthorizedClient(await UserAsync(_factory, "l1-other"));
            using var hr = _factory.CreateAuthorizedClient(await UserAsync(_factory, "l1-hr", approve: ["hr-leave"]));
            (await admin.PostFormAsync("/api/ShiftAssignment/BulkAssign", ("EmployeeIds", e1), ("EmployeeIds", e2), ("ShiftId", w.Trua), ("FromDate", Monday), ("ToDate", Monday.AddDays(4)))).Ok();

            (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(1)), ("ToDate", Monday.AddDays(2)), ("LeaveTypeCode", "XYZ"), ("Reason", "Việc nhà"))).Fail("Loại nghỉ");
            (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(2)), ("ToDate", Monday.AddDays(1)), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Việc nhà"))).Fail("đến ngày");
            (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(1)), ("ToDate", Monday.AddDays(2)), ("LeaveTypeCode", "ANNUAL"))).Fail("lý do");
            (await staff.PostFormAsync("/api/LeaveRequest/Create", ("EmployeeId", e2), ("FromDate", Monday), ("ToDate", Monday), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Hộ"))).Fail("Không có quyền");
            (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Today.AddDays(-9)), ("ToDate", Today.AddDays(-8)), ("LeaveTypeCode", "SICK"), ("Reason", "Ốm"))).Fail("lùi tối đa 7");

            var created = await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(1)), ("ToDate", Monday.AddDays(2)), ("LeaveTypeCode", "annual"), ("Reason", "Về quê"));
            var leave = created.Ok();
            Guid leaveId = leave.GetProperty("request").GetProperty("id").GetGuid();
            leave.GetProperty("request").GetProperty("requestNo").GetString().Should().StartWith("DN-261210-");
            leave.GetProperty("request").GetProperty("requestStatus").GetString().Should().Be("Pending");
            leave.GetProperty("request").GetProperty("leaveTypeCode").GetString().Should().Be("ANNUAL");
            leave.GetProperty("leaveTypeName").GetString().Should().Be("Nghỉ phép năm");
            leave.GetProperty("days").GetInt32().Should().Be(2);
            leave.GetProperty("listWarning").EnumerateArray().Should().HaveCount(2).And.OnlyContain(x => x.GetString()!.Contains("sẽ bỏ ca"));
            leave.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().Equal("cancel");
            leave.GetProperty("pendingApproval").GetProperty("screenId").GetString().Should().Be("hr-leave");
            var approval = await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == leaveId));
            approval.BusinessType.Should().Be("LEAVE_REQUEST");
            (await _factory.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync(l => l.Id == leaveId))).ApprovalRequestId.Should().Be(approval.Id);
            (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(2)), ("ToDate", Monday.AddDays(3)), ("LeaveTypeCode", "SICK"), ("Reason", "Bị ốm"))).Fail("trùng khoảng ngày");

            // Hộp duyệt "Duyệt – Đơn xin nghỉ": người không có quyền không duyệt được, nhân sự duyệt → ca trong khoảng nghỉ bị bỏ.
            (await other.PostFormAsync("/api/LeaveRequest/Approve", ("Id", leaveId))).Fail("không có quyền");
            (await other.GetResultAsync($"/api/LeaveRequest/GetDetail?id={leaveId}")).Fail("Không có quyền xem");
            var inbox = (await hr.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=hr-leave")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == approval.Id);
            inbox.GetProperty("title").GetString().Should().Contain("Đơn nghỉ DN-").And.Contain("NV 1 L1").And.Contain("bỏ 2 ca");
            (await hr.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={approval.Id}")).Ok().GetProperty("business").GetProperty("table").GetString().Should().Be("LeaveRequest");
            (await hr.PostFormAsync("/api/LeaveRequest/Approve", ("Id", leaveId), ("Note", "Đồng ý"))).Ok();
            var detail = (await staff.GetResultAsync($"/api/LeaveRequest/GetDetail?id={leaveId}")).Ok();
            detail.GetProperty("request").GetProperty("requestStatus").GetString().Should().Be("Approved");
            detail.GetProperty("listWarning").EnumerateArray().Should().HaveCount(2).And.OnlyContain(x => x.GetString()!.Contains("thiếu người"));
            var rows = await RowsAsync(e1);
            rows.Where(r => r.WorkDate >= Monday.AddDays(1) && r.WorkDate <= Monday.AddDays(2)).Should().OnlyContain(r => r.IsDeleted && r.Note!.Contains("Nghỉ theo đơn DN-"));
            rows.Count(r => !r.IsDeleted).Should().Be(3);
            var grid = (await admin.GetResultAsync($"/api/ShiftAssignment/GetGrid?FromDate=2026-12-14&OrganizationUnitId={w.ParentOu}")).Ok();
            var cells = grid.GetProperty("employees").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == e1).GetProperty("cells");
            cells.GetProperty("2026-12-15").GetProperty("onLeave").GetBoolean().Should().BeTrue();
            cells.GetProperty("2026-12-15").GetProperty("assignmentId").ValueKind.Should().Be(JsonValueKind.Null);
            (await Assign(admin, e1, Monday.AddDays(1), w.Trua)).Fail("nghỉ phép");
            (await staff.PostFormAsync("/api/LeaveRequest/Cancel", ("Id", leaveId), ("Reason", "Thôi"))).Fail("Chờ duyệt");

            // Từ chối (lý do bắt buộc) → Từ chối, ca giữ nguyên.
            var second = (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(4)), ("ToDate", Monday.AddDays(4)), ("LeaveTypeCode", "PERSONAL"), ("Reason", "Việc riêng"))).Ok();
            Guid secondId = second.GetProperty("request").GetProperty("id").GetGuid();
            (await hr.PostFormAsync("/api/LeaveRequest/Reject", ("Id", secondId))).Fail();
            (await hr.PostFormAsync("/api/LeaveRequest/Reject", ("Id", secondId), ("Note", "Thiếu người"))).Ok();
            (await _factory.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync(l => l.Id == secondId))).RequestStatus.Should().Be("Rejected");
            (await RowsAsync(e1)).Single(r => r.WorkDate == Monday.AddDays(4)).IsDeleted.Should().BeFalse();

            // Hủy: người khác không hủy được; người tạo hủy → Đã hủy + yêu cầu duyệt Đã hủy.
            var third = (await staff.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(4)), ("ToDate", Monday.AddDays(4)), ("LeaveTypeCode", "PERSONAL"), ("Reason", "Việc riêng 2"))).Ok();
            Guid thirdId = third.GetProperty("request").GetProperty("id").GetGuid();
            (await other.PostFormAsync("/api/LeaveRequest/Cancel", ("Id", thirdId), ("Reason", "x"))).Fail();
            (await staff.PostFormAsync("/api/LeaveRequest/Cancel", ("Id", thirdId), ("Reason", "Đổi kế hoạch"))).Ok();
            (await _factory.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync(l => l.Id == thirdId))).RequestStatus.Should().Be("Cancelled");
            (await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == thirdId))).RequestStatus.Should().Be("Cancelled");
            (await staff.GetResultAsync("/api/LeaveRequest/GetMine")).ListData.EnumerateArray().Should().HaveCount(3);
            (await admin.PostFormAsync("/api/LeaveRequest/Insert", ("RequestNo", "X"), ("EmployeeId", e1), ("FromDate", Monday), ("ToDate", Monday), ("LeaveTypeCode", "ANNUAL"))).Fail("LeaveRequest/Create");

            // Ngày đã chấm công không xin nghỉ được (nhân sự lập hộ cho nhân viên 2).
            await _factory.SeedAsync(db =>
            {
                var a = db.ShiftAssignment.Single(x => x.EmployeeId == e2 && x.WorkDate == Monday && !x.IsDeleted);
                db.AttendanceEvent.Add(new AttendanceEvent { Id = Guid.NewGuid(), EmployeeId = e2, ShiftAssignmentId = a.Id, EventType = "CHECK_IN", EventAt = Monday.AddHours(10), SourceChannel = "MACHINE", IsValid = true });
                return Task.CompletedTask;
            });
            (await admin.PostFormAsync("/api/LeaveRequest/Create", ("EmployeeId", e2), ("FromDate", Monday), ("ToDate", Monday), ("LeaveTypeCode", "SICK"), ("Reason", "Ốm giữa ca"))).Fail("đã chấm công");
        }

        [Fact]
        public async Task Shift_swap_and_give_away_are_applied_on_approval_and_respect_locks()
        {
            var w = await WorldAsync("S1");
            using var admin = _factory.CreateAuthorizedClient();
            Guid e1 = w.Employees[0], e2 = w.Employees[1], e3 = w.Employees[2];
            using var staff1 = _factory.CreateAuthorizedClient(await UserAsync(_factory, w.Codes[0]));
            using var staff2 = _factory.CreateAuthorizedClient(await UserAsync(_factory, w.Codes[1]));
            using var hr = _factory.CreateAuthorizedClient(await UserAsync(_factory, "s1-hr", approve: ["hr-swap"]));
            Guid a = (await Assign(admin, e1, Monday, w.Trua)).Ok().GetProperty("assignment").GetProperty("id").GetGuid();
            Guid b = (await Assign(admin, e2, Monday.AddDays(1), w.Toi)).Ok().GetProperty("assignment").GetProperty("id").GetGuid();
            Guid e2Monday = (await Assign(admin, e2, Monday, w.Dem)).Ok().GetProperty("assignment").GetProperty("id").GetGuid();

            // Hoán đổi: người nhận đã có ca khác ngày 14/12 → 400; bỏ ca đêm của nhân viên 2 rồi gửi lại.
            (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a), ("ProposedShiftAssignmentId", b), ("Reason", "Bận việc"))).Fail("đã có ca khác ngày 14/12/2026");
            (await Assign(admin, e2, Monday, null)).Ok();
            (await staff2.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a), ("ProposedShiftAssignmentId", b), ("Reason", "Bận"))).Fail("Không có quyền");
            (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a), ("ProposedShiftAssignmentId", b))).Fail("lý do");
            (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a), ("ProposedShiftAssignmentId", b), ("TargetEmployeeId", e3), ("Reason", "Bận"))).Fail("Người nhận không phải");
            var swap = (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a), ("ProposedShiftAssignmentId", b), ("Reason", "Bận việc gia đình"))).Ok();
            Guid swapId = swap.GetProperty("request").GetProperty("id").GetGuid();
            swap.GetProperty("swapType").GetString().Should().Be("SWAP");
            swap.GetProperty("request").GetProperty("requestNo").GetString().Should().StartWith("DC-261210-");
            swap.GetProperty("targetName").GetString().Should().Be("NV 2 S1");
            (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", a), ("TargetEmployeeId", e3), ("Reason", "Bận"))).Fail("đơn đổi ca khác");
            (await Assign(admin, e1, Monday, w.Toi)).Fail("đơn đổi ca chờ duyệt");
            var grid = (await admin.GetResultAsync($"/api/ShiftAssignment/GetGrid?FromDate=2026-12-14&OrganizationUnitId={w.ParentOu}")).Ok();
            grid.GetProperty("employees").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == e1).GetProperty("cells").GetProperty("2026-12-14")
                .GetProperty("pendingSwap").GetBoolean().Should().BeTrue();
            var approval = await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(x => x.BusinessId == swapId));
            approval.BusinessType.Should().Be("SHIFT_SWAP");
            var inbox = (await hr.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=hr-swap")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == approval.Id);
            inbox.GetProperty("title").GetString().Should().Contain("Đổi ca DC-").And.Contain("S1-TRUA 14/12/2026").And.Contain("S1-TOI 15/12/2026");
            (await staff2.GetResultAsync($"/api/ShiftSwapRequest/GetDetail?id={swapId}")).Ok(); // người nhận xem được
            (await hr.PostFormAsync("/api/ShiftSwapRequest/Approve", ("Id", swapId), ("Note", "OK"))).Ok();
            var rowA = await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().SingleAsync(x => x.Id == a));
            var rowB = await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().SingleAsync(x => x.Id == b));
            rowA.EmployeeId.Should().Be(e2);
            rowB.EmployeeId.Should().Be(e1);
            rowA.Note.Should().Contain("Đổi ca theo đơn DC-");
            (await _factory.QueryAsync(db => db.ShiftSwapRequest.AsNoTracking().SingleAsync(x => x.Id == swapId))).RequestStatus.Should().Be("Approved");

            // Nhường ca: người nhận đã có ca khác cùng ngày → 400.
            Guid c = (await Assign(admin, e3, Monday.AddDays(2), w.Trua)).Ok().GetProperty("assignment").GetProperty("id").GetGuid();
            (await Assign(admin, e1, Monday.AddDays(2), w.Toi)).Ok();
            (await admin.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", c), ("TargetEmployeeId", e1), ("Reason", "Nhường"))).Fail("đã có ca khác");
            // Dòng (nhân viên 2, TRUA, 16/12) đã xóa mềm từ trước → khi nhường cho nhân viên 2 phải dùng lại dòng đó (UNIQUE kể cả dòng xóa mềm).
            (await Assign(admin, e2, Monday.AddDays(2), w.Trua)).Ok();
            (await Assign(admin, e2, Monday.AddDays(2), null)).Ok();
            var give = (await admin.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", c), ("TargetEmployeeId", e2), ("Reason", "Nhường ca cho bạn"))).Ok();
            Guid giveId = give.GetProperty("request").GetProperty("id").GetGuid();
            give.GetProperty("swapType").GetString().Should().Be("GIVE");
            (await hr.PostFormAsync("/api/ShiftSwapRequest/Approve", ("Id", giveId))).Ok();
            var e2Rows = (await RowsAsync(e2)).Where(r => r.WorkDate == Monday.AddDays(2) && r.ShiftId == w.Trua).ToList();
            e2Rows.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
            (await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().SingleAsync(x => x.Id == c))).IsDeleted.Should().BeTrue("dòng của người nhường xóa mềm");

            // Ca đã chấm công không đổi được; hủy đơn bởi người tạo.
            _factory.Clock.FixedNow = Monday.AddDays(1).AddHours(16).AddMinutes(30);
            (await staff1.PostFormAsync("/api/AttendanceEvent/CheckIn", ("SourceChannel", "QR"), ("WorkstationCode", "S1-KHONG"))).Fail("Không tìm thấy trạm");
            await _factory.SeedAsync(db =>
            {
                db.AttendanceEvent.Add(new AttendanceEvent { Id = Guid.NewGuid(), EmployeeId = e1, ShiftAssignmentId = b, EventType = "CHECK_IN", EventAt = Monday.AddDays(1).AddHours(16.5), SourceChannel = "MACHINE", IsValid = true });
                return Task.CompletedTask;
            });
            (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", b), ("TargetEmployeeId", e3), ("Reason", "Về sớm"))).Fail("chấm công");
            _factory.Clock.FixedNow = Today.AddHours(8);
            Guid d = (await Assign(admin, e1, Monday.AddDays(3), w.Trua)).Ok().GetProperty("assignment").GetProperty("id").GetGuid();
            var pending = (await staff1.PostFormAsync("/api/ShiftSwapRequest/Create", ("CurrentShiftAssignmentId", d), ("TargetEmployeeId", e3), ("Reason", "Có việc"))).Ok();
            Guid pendingId = pending.GetProperty("request").GetProperty("id").GetGuid();
            (await staff2.PostFormAsync("/api/ShiftSwapRequest/Cancel", ("Id", pendingId), ("Reason", "x"))).Fail();
            (await staff1.PostFormAsync("/api/ShiftSwapRequest/Cancel", ("Id", pendingId), ("Reason", "Xong việc"))).Ok();
            (await _factory.QueryAsync(db => db.ShiftSwapRequest.AsNoTracking().SingleAsync(x => x.Id == pendingId))).RequestStatus.Should().Be("Cancelled");
            (await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().SingleAsync(x => x.Id == d))).EmployeeId.Should().Be(e1);
            (await staff1.GetResultAsync("/api/ShiftSwapRequest/GetMine")).ListData.EnumerateArray().Should().HaveCount(2);
        }
    }
}
