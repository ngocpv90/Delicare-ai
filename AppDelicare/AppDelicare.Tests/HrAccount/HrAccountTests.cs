using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Identity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.HrAccount
{
    /// <summary>
    /// HR-ACC-1.9 (D-82): tài khoản ↔ con người ↔ nhân viên + vòng đời tài khoản.
    /// - Đổi tên đăng nhập / mã NV / SĐT vẫn tra đúng nhân viên (liên kết thật Employee.PersonId); tắt đoán cũ thì không đoán.
    /// - 2 nhân viên trùng SĐT: Đối chiếu báo nhiều ứng viên, gắn tay đúng người, người đã gắn không gắn lần 2.
    /// - Cấp tài khoản qua duyệt (IT đổi nhóm trước khi Duyệt) → User INTERNAL + nhóm + liên kết + mật khẩu tạm xem 1 lần; không tạo trùng yêu cầu.
    /// - Nghỉ việc → khóa ngay: đăng nhập + API (token đang có) + bot bị chặn, yêu cầu đang giữ leo thang, yêu cầu đã gửi báo người duyệt.
    /// - Tài khoản CUSTOMER bị chặn đăng nhập + API nội bộ kể cả khi IsAdmin.
    /// - Sửa nhân viên ↔ hồ sơ Person ghi cả hai; người duyệt không duyệt yêu cầu về chính mình.
    /// </summary>
    /// <summary>Helper dùng chung (mỗi lớp test con = 1 DB InMemory riêng qua IClassFixture).</summary>
    public abstract class HrAccountTestBase
    {
        protected static readonly DateTime Today = new(2026, 12, 10);
        protected readonly InMemoryApiFactory _factory;

        protected HrAccountTestBase(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(9);
        }

        protected async Task<Guid> UserAsync(string userName, string[]? approve = null, string[]? groups = null, Guid? personId = null, string? password = null,
            string accountType = "INTERNAL", bool isAdmin = false)
        {
            Guid userId = Guid.NewGuid(), groupId = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User
                {
                    Id = userId, UserName = userName, IsActive = true, PersonId = personId, AccountType = accountType, IsAdmin = isAdmin,
                    PassWord = password == null ? null : EncriptFunctionsHelper.GeneratePassword(password),
                });
                db.UserGroup.Add(new UserGroup { Id = groupId, Name = "HA-" + groupId.ToString("N")[..8] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
                foreach (string name in groups ?? [])
                {
                    var g = await db.UserGroup.FirstOrDefaultAsync(x => x.Name == name) ?? db.UserGroup.Local.FirstOrDefault(x => x.Name == name);
                    if (g == null)
                    {
                        g = new UserGroup { Id = Guid.NewGuid(), Name = name };
                        db.UserGroup.Add(g);
                    }
                    db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = g.Id });
                }
                foreach (string link in (approve ?? []).Select(s => s.StartsWith("#/") ? s : "#/wf-inbox?type=" + s))
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = groupId, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = true, AllowAdd = true });
                }
            });
            return userId;
        }

        protected async Task GroupsAsync(params string[] names)
            => await _factory.SeedAsync(async db =>
            {
                foreach (string n in names)
                {
                    if (!await db.UserGroup.AnyAsync(g => g.Name == n) && !db.UserGroup.Local.Any(g => g.Name == n))
                    {
                        db.UserGroup.Add(new UserGroup { Id = Guid.NewGuid(), Name = n });
                    }
                }
            });

        protected async Task<Guid> EmployeeAsync(string code, string? phone = null, Guid? personId = null, string status = "Active")
        {
            Guid id = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = id, Code = code, EmployeeCode = code, FullName = "NV " + code, Phone = phone, PersonId = personId, Status = status });
                return Task.CompletedTask;
            });
            return id;
        }

        protected async Task<Guid> PersonAsync(string name, string? phone = null)
        {
            Guid id = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Person.Add(new Person { Id = id, FullName = name, PhoneNumber = phone });
                return Task.CompletedTask;
            });
            return id;
        }

        protected static async Task<(int Code, bool HasToken, string? Message)> LoginAsync(HttpClient client, string userName, string password)
        {
            var response = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName, passWord = password });
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            int code = doc.RootElement.GetProperty("code").GetInt32();
            bool hasToken = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("token", out var token) && !string.IsNullOrEmpty(token.GetString());
            return (code, hasToken, doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null);
        }

    }

    public class HrAccountTests(InMemoryApiFactory factory) : HrAccountTestBase(factory), IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public async Task Linked_employee_survives_rename_of_username_code_and_phone_and_legacy_guess_can_be_switched_off()
        {
            Guid person = await PersonAsync("Người HA1", "0901 111 001");
            Guid emp = await EmployeeAsync("HA1-001", "0901111001", person);
            Guid user = await UserAsync("ha1.login", personId: person);
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, user, legacy: false)))!.Id.Should().Be(emp);

            // Đổi tên đăng nhập, mã NV, SĐT – liên kết thật không phụ thuộc 3 thứ này.
            await _factory.SeedAsync(async db =>
            {
                (await db.User.SingleAsync(u => u.Id == user)).UserName = "ha1.renamed";
                var e = await db.Employee.SingleAsync(x => x.Id == emp);
                e.Code = "HA1-999";
                e.Phone = "0988 000 999";
            });
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, user, legacy: false)))!.Id.Should().Be(emp);
            (await _factory.QueryAsync(db => HrRules.ResolveEmployeeAsync(db, user)))!.Id.Should().Be(emp, "HrRules gọi PersonLink");
            var reverse = await _factory.QueryAsync(async db => await PersonLink.UserOfEmployeeAsync(db, await db.Employee.AsNoTracking().SingleAsync(e => e.Id == emp), legacy: false));
            reverse!.Id.Should().Be(user);

            // Chưa gắn: đoán cũ (tên đăng nhập = mã NV) chỉ khi bật cờ; nhân viên đã gắn người khác thì không bao giờ đoán sang.
            Guid legacyEmp = await EmployeeAsync("HA1-LEG");
            Guid legacyUser = await UserAsync("HA1-LEG");
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, legacyUser, legacy: true)))!.Id.Should().Be(legacyEmp);
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, legacyUser, legacy: false))).Should().BeNull("tắt BusinessRules:Hr:LegacyEmployeeMatch");
            Guid otherUser = await UserAsync("HA1-999"); // trùng mã mới của nhân viên đã gắn người khác
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, otherUser, legacy: true))).Should().BeNull();
        }

        [Fact]
        public async Task Two_employees_with_the_same_phone_are_reviewed_and_linked_to_the_right_person()
        {
            Guid person = await PersonAsync("Người HA2", "+84 902 222 002");
            Guid user = await UserAsync("ha2.user", personId: person);
            Guid e1 = await EmployeeAsync("HA2-001", "0902222002");
            Guid e2 = await EmployeeAsync("HA2-002", "0902.222.002");
            using var admin = _factory.CreateAuthorizedClient();

            var review = (await admin.GetResultAsync("/api/Employee/GetLinkReview")).Ok();
            var rows = review.GetProperty("listRow").EnumerateArray().Where(r => r.GetProperty("employeeId").GetGuid() is var id && (id == e1 || id == e2)).ToList();
            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(r => r.GetProperty("group").GetString() == "MULTI", "1 người khớp SĐT của 2 nhân viên → không gắn tự động");
            rows[0].GetProperty("listCandidate")[0].GetProperty("userName").GetString().Should().Be("ha2.user");

            var link = await admin.PostAsync("/api/Employee/LinkPerson", JsonContent.Create(new { employeeId = e2, userId = user }));
            (await LegacyResult.ReadAsync(link)).Ok();
            var again = await admin.PostAsync("/api/Employee/LinkPerson", JsonContent.Create(new { employeeId = e1, personId = person }));
            (await LegacyResult.ReadAsync(again)).Fail("đã gắn nhân viên");
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, user, legacy: false)))!.Id.Should().Be(e2);

            // Gỡ rồi gắn lại người khác vẫn được.
            (await LegacyResult.ReadAsync(await admin.PostAsync("/api/Employee/LinkPerson", JsonContent.Create(new { employeeId = e2, unlink = true })))).Ok();
            (await _factory.QueryAsync(db => db.Employee.AsNoTracking().Where(e => e.Id == e2).Select(e => e.PersonId).SingleAsync())).Should().BeNull();
        }

        [Fact]
        public async Task Account_request_is_approved_by_it_with_changed_groups_and_creates_user_link_and_temp_password()
        {
            await GroupsAsync("HA3-KIT", "HA3-QA");
            Guid emp = await EmployeeAsync("HA3-001", "0903333001");
            using var admin = _factory.CreateAuthorizedClient();
            using var it = _factory.CreateAuthorizedClient(await UserAsync("ha3.it", ["adm-user"]));

            var sent = await admin.PostAsync("/api/Employee/RequestAccount", JsonContent.Create(new { employeeId = emp, groups = "HA3-KIT", reason = "Tuyển mới bếp" }));
            Guid requestId = (await LegacyResult.ReadAsync(sent)).Ok().GetGuid();
            (await LegacyResult.ReadAsync(await admin.PostAsync("/api/Employee/RequestAccount", JsonContent.Create(new { employeeId = emp })))).Fail("đang chờ");

            var account = (await admin.GetResultAsync($"/api/Employee/GetAccount?id={emp}")).Ok();
            account.GetProperty("status").GetString().Should().Be(AccountLifecycleCodes.StatusPending);

            var pending = (await it.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == requestId);
            var summary = pending.GetProperty("summary");
            summary.GetProperty("title").GetString().Should().Contain("Cấp tài khoản").And.Contain("HA3-001");
            summary.GetProperty("reason").GetString().Should().Be("Tuyển mới bếp");
            var edit = summary.GetProperty("edit");
            edit.GetProperty("selected").EnumerateArray().Select(x => x.GetString()).Should().Equal("HA3-KIT");
            edit.GetProperty("options").EnumerateArray().Select(x => x.GetString()).Should().Contain("HA3-QA");

            (await it.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", requestId), ("Approve", true), ("Proposal", "{\"groups\":[\"KHONG-CO\"]}"))).Fail("Không có nhóm quyền");
            (await it.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", requestId), ("Approve", true), ("Proposal", "{\"groups\":[\"HA3-QA\"]}"))).Ok();

            var user = await _factory.QueryAsync(db => db.User.AsNoTracking().SingleAsync(u => u.UserName == "ha3-001"));
            user.AccountType.Should().Be(PersonLink.Internal);
            user.IsActive.Should().BeTrue();
            EncriptFunctionsHelper.IsTemporary(user.PassWord).Should().BeTrue("mật khẩu tạm, bắt đổi lần đầu (T45R.8)");
            var e = await _factory.QueryAsync(db => db.Employee.AsNoTracking().SingleAsync(x => x.Id == emp));
            e.PersonId.Should().Be(user.PersonId!.Value);
            (await _factory.QueryAsync(db => db.Person.AsNoTracking().SingleAsync(p => p.Id == user.PersonId))).FullName.Should().Be("NV HA3-001");
            var groups = await _factory.QueryAsync(db => (from c in db.UserClaim join g in db.UserGroup on c.UserGroupId equals g.Id
                                                          where c.UserId == user.Id && !c.IsDeleted select g.Name).ToListAsync());
            groups.Should().Equal(["HA3-QA"], "IT đổi nhóm trước khi duyệt – đề xuất mới lưu vào PayloadJson");
            (await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().Where(a => a.Id == requestId).Select(a => a.PayloadJson).SingleAsync())).Should().Contain("HA3-QA");

            var created = (await it.GetResultAsync($"/api/User/TakeCreatedAccount?employeeId={emp}")).Ok();
            string temp = created.GetProperty("temporaryPassword").GetString()!;
            temp.Should().NotBeNullOrEmpty("nhân viên chưa liên kết bot → IT xem 1 lần");
            (await it.GetResultAsync($"/api/User/TakeCreatedAccount?employeeId={emp}")).Fail("Không còn mật khẩu tạm");
            using var anon = _factory.CreateClient();
            (await LoginAsync(anon, "ha3-001", temp)).Should().Match<(int Code, bool HasToken, string? Message)>(r => r.Code == 200 && r.HasToken);

            (await admin.GetResultAsync($"/api/Employee/GetAccount?id={emp}")).Ok().GetProperty("status").GetString().Should().Be(AccountLifecycleCodes.StatusActive);
            (await LegacyResult.ReadAsync(await admin.PostAsync("/api/Employee/RequestAccount", JsonContent.Create(new { employeeId = emp })))).Fail("đã có tài khoản");
        }

        [Fact]
        public async Task It_creates_account_directly_and_account_screen_and_my_work_show_the_link()
        {
            await GroupsAsync("HA7-KIT");
            Guid emp = await EmployeeAsync("HA7-001", "0907777001");
            using var it = _factory.CreateAuthorizedClient(await UserAsync("ha7.it", ["#/adm-user", "#/adm-employee"]));
            var created = (await LegacyResult.ReadAsync(await it.PostAsync("/api/User/CreateForEmployee", JsonContent.Create(new { employeeId = emp, groups = "HA7-KIT" })))).Ok();
            created.GetProperty("userName").GetString().Should().Be("ha7-001");
            created.GetProperty("temporaryPassword").GetString().Should().NotBeNullOrEmpty();
            Guid user = created.GetProperty("userId").GetGuid();

            var links = (await it.GetResultAsync("/api/User/GetAccountLinks")).Ok();
            var row = links.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("userId").GetGuid() == user);
            (row.GetProperty("kindName").GetString(), row.GetProperty("employeeCode").GetString(), row.GetProperty("linked").GetBoolean()).Should().Be(("Nhân viên", "HA7-001", true));
            links.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("userName").GetString() == "ha7.it").GetProperty("kindName").GetString().Should().Be("Hệ thống");

            using var me = _factory.CreateAuthorizedClient(user);
            (await me.GetResultAsync("/api/User/GetMyEmployee")).Ok().GetProperty("linked").GetBoolean().Should().BeTrue();
            (await it.GetResultAsync("/api/User/GetMyEmployee")).Ok().GetProperty("linked").GetBoolean().Should().BeFalse("tài khoản IT chưa gắn nhân viên");
            (await it.GetResultAsync("/api/Employee/GetPositionGroups")).Ok().ValueKind.Should().Be(JsonValueKind.Object);
        }

        [Fact]
        public async Task Customer_account_is_blocked_from_internal_login_and_api_even_as_admin()
        {
            Guid customer = await UserAsync("ha5.customer", password: "Mat-khau-HA5", accountType: PersonLink.Customer, isAdmin: true);
            using var anon = _factory.CreateClient();
            var login = await LoginAsync(anon, "ha5.customer", "Mat-khau-HA5");
            login.HasToken.Should().BeFalse();
            login.Message.Should().Be(ConstantHelper.Message.AccountNotInternal);

            using var client = _factory.CreateAuthorizedClient(customer); // token cấp ngoài màn đăng nhập (vd. cổng khách sau này)
            (await client.GetAsync("/api/Shift/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync("/api/User/GetCurrent")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "kể cả API tự phục vụ");
            (await _factory.QueryAsync(db => PersonLink.EmployeeOfUserAsync(db, customer))).Should().BeNull("tài khoản khách không có nhân viên");
        }

        [Fact]
        public async Task Employee_and_person_edits_write_both_and_approver_cannot_decide_own_request()
        {
            Guid person = await PersonAsync("Người HA6", "0906666006");
            Guid emp = await EmployeeAsync("HA6-001", "0906666006", person);
            Guid self = await UserAsync("ha6.self", ["hr-leave"], personId: person);
            using var admin = _factory.CreateAuthorizedClient();

            (await LegacyResult.ReadAsync(await admin.PutAsync("/api/Employee/Update", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = emp.ToString(), ["FullName"] = "Tên mới HA6", ["Phone"] = "0906 666 999", ["Status"] = "Active",
            })))).Ok();
            var p = await _factory.QueryAsync(db => db.Person.AsNoTracking().SingleAsync(x => x.Id == person));
            (p.FullName, p.PhoneNumber).Should().Be(("Tên mới HA6", "0906 666 999"), "sửa nhân viên ghi cả Person (D-82 e)");

            using var me = _factory.CreateAuthorizedClient(self);
            (await me.PutAsync("/api/Person/UpdateProfile", new MultipartFormDataContent { { new StringContent("Tên tự sửa HA6"), "FullName" }, { new StringContent("0906 000 111"), "PhoneNumber" } }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            var e = await _factory.QueryAsync(db => db.Employee.AsNoTracking().SingleAsync(x => x.Id == emp));
            (e.FullName, e.Phone).Should().Be(("Tên tự sửa HA6", "0906 000 111"), "sửa hồ sơ cá nhân ghi cả Employee");

            // HR lập đơn nghỉ cho chính người duyệt → người đó không duyệt được (kể cả không phải người gửi).
            (await admin.PostFormAsync("/api/LeaveRequest/Create", ("EmployeeId", emp), ("FromDate", Today.AddDays(6)), ("ToDate", Today.AddDays(6)), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Khám bệnh"))).Ok();
            Guid rid = await _factory.QueryAsync(db => db.ApprovalRequest.Where(a => a.BusinessType == LeaveRequestStatus.BusinessType && db.LeaveRequest.Any(l => l.Id == a.BusinessId && l.EmployeeId == emp)).Select(a => a.Id).SingleAsync());
            (await me.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", rid), ("Approve", true))).Fail(ConstantHelper.Message.ApprovalSubjectSelf);
        }
    }

    /// <summary>DB riêng: người nghỉ việc là người duyệt DUY NHẤT của đơn nghỉ (lớp khác có thêm người duyệt hr-leave).</summary>
    public class HrAccountResignTests(InMemoryApiFactory factory) : HrAccountTestBase(factory), IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public async Task Resigning_locks_login_api_and_bot_immediately_and_escalates_held_requests()
        {
            Guid person = await PersonAsync("Người HA4");
            Guid emp = await EmployeeAsync("HA4-001", null, person);
            Guid leaver = await UserAsync("ha4.leaver", ["hr-leave"], personId: person, password: "Mat-khau-HA4");
            Guid coo = await UserAsync("ha4.coo", groups: ["COO"]);
            Guid staffEmp = await EmployeeAsync("HA4-STAFF");
            await _factory.SeedAsync(db =>
            {
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = leaver, EmployeeId = emp, IntegrationChannelId = Guid.NewGuid(), ExternalUserId = "ha4-tg", LinkedAt = Today, IdentityStatus = "Active" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            using var leaverClient = _factory.CreateAuthorizedClient(leaver);
            (await leaverClient.GetAsync("/api/User/GetCurrent")).StatusCode.Should().Be(HttpStatusCode.OK);

            // Đơn nghỉ của nhân viên khác chỉ người nghỉ việc duyệt được (người duyệt duy nhất).
            (await admin.PostFormAsync("/api/LeaveRequest/Create", ("EmployeeId", staffEmp), ("FromDate", Today.AddDays(5)), ("ToDate", Today.AddDays(5)), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Việc riêng"))).Ok();
            Guid held = await _factory.QueryAsync(db => db.ApprovalRequest.Where(a => a.BusinessType == LeaveRequestStatus.BusinessType && db.LeaveRequest.Any(l => l.Id == a.BusinessId && l.EmployeeId == staffEmp)).Select(a => a.Id).SingleAsync());

            var update = await admin.PutAsync("/api/Employee/Update", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = emp.ToString(), ["FullName"] = "NV HA4-001", ["Status"] = EmployeeStatus.Resigned,
            }));
            (await LegacyResult.ReadAsync(update)).Ok();

            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == leaver).Select(u => u.IsActive).SingleAsync())).Should().BeFalse();
            (await leaverClient.GetAsync("/api/User/GetCurrent")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "token đang có bị chặn ngay");
            using var anon = _factory.CreateClient();
            (await LoginAsync(anon, "ha4.leaver", "Mat-khau-HA4")).HasToken.Should().BeFalse("đăng nhập bị chặn");
            (await _factory.QueryAsync(db => db.UserExternalIdentity.AsNoTracking().Where(i => i.UserId == leaver).Select(i => i.IdentityStatus).SingleAsync())).Should().Be("Revoked", "bot bị chặn");
            (await _factory.QueryAsync(db => db.ApprovalItem.AsNoTracking().AnyAsync(i => i.ApprovalRequestId == held && i.ActionCode == ApprovalRequestService.AbsentActionCode)))
                .Should().BeTrue("yêu cầu người nghỉ đang giữ (không còn ai duyệt) → leo thang bậc kế (COO)");
            _ = coo;

            // Job hằng ngày: không còn gì để khóa.
            var job = (await admin.PostFormAsync("/api/Job/Run", ("jobCode", "AccountLockResigned"))).Ok();
            (await admin.GetResultAsync("/api/User/GetAccountLinks")).Ok().GetProperty("resignedButActiveCount").GetInt32().Should().Be(0);
            _ = job;

            // Quay lại làm → yêu cầu "Mở lại tài khoản" cho FIN-IT; duyệt → mở.
            (await LegacyResult.ReadAsync(await admin.PutAsync("/api/Employee/Update", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = emp.ToString(), ["FullName"] = "NV HA4-001", ["Status"] = EmployeeStatus.Active,
            })))).Ok();
            Guid reopen = await _factory.QueryAsync(db => db.ApprovalRequest.Where(a => a.BusinessType == AccountLifecycleCodes.ReopenBusinessType && a.BusinessId == emp).Select(a => a.Id).SingleAsync());
            (await admin.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", reopen), ("Approve", true))).Ok();
            (await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == leaver).Select(u => u.IsActive).SingleAsync())).Should().BeTrue();
        }

    }
}
