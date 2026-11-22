using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// UI-5 (D-63 r): form khách hàng – danh mục Ngành / Quy mô / Giới tính / Nguồn (máy chủ chặn mã lạ), NV phụ trách = tài khoản đang hoạt động,
    /// khách doanh nghiệp tự có liên hệ chính, điểm giao đầu tiên tạo cùng lúc (lỗi giờ → không tạo khách), API dữ liệu form.
    /// </summary>
    public class CustomerFormTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public CustomerFormTests(InMemoryApiFactory factory) => _factory = factory;

        private async Task<(Guid Branch, Guid Active, Guid Inactive)> SeedAsync()
        {
            Guid branch = Guid.NewGuid(), kitchen = Guid.NewGuid(), active = Guid.NewGuid(), inactive = Guid.NewGuid(), person = Guid.NewGuid();
            string tag = branch.ToString("N")[..6].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = branch, CompanyId = Guid.NewGuid(), Code = "CN-" + tag, Name = "[DEMO] Chi nhánh " + tag, UnitType = "BRANCH" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = kitchen, CompanyId = Guid.NewGuid(), Code = "BEP-" + tag, Name = "[DEMO] Bếp " + tag, UnitType = "KITCHEN" });
                db.Person.Add(new Person { Id = person, FullName = "[DEMO] Trần Kinh Doanh " + tag });
                db.User.Add(new User { Id = active, UserName = "kd-" + tag, PersonId = person, IsActive = true });
                db.User.Add(new User { Id = inactive, UserName = "nghi-" + tag, IsActive = false });
                return Task.CompletedTask;
            });
            return (branch, active, inactive);
        }

        private static (string, object?)[] Business(Guid branch, string name, string tax, params (string, object?)[] extra) =>
        [
            ("CustomerType", "BUSINESS"), ("LegalName", name), ("DisplayName", name), ("TaxCode", tax), ("LegalRepresentativeName", "[DEMO] Ông A"),
            ("ContactPersonName", "[DEMO] Chị B"), ("ContactPersonPhone", "0901 234 567"), ("ContactPersonEmail", "b@demo.vn"), ("Address", "[DEMO] Q1"),
            ("PhoneNumber", "0281234567"), ("BranchId", branch), .. extra,
        ];

        [Fact]
        public async Task GetFormOptions_lists_only_branch_units_active_users_with_person_names_and_catalogs()
        {
            var (branch, active, inactive) = await SeedAsync();
            using var c = _factory.CreateAuthorizedClient();
            var d = (await c.GetResultAsync("/api/Customer/GetFormOptions")).Ok();
            d.GetProperty("branchRequired").GetBoolean().Should().BeTrue();
            var branches = d.GetProperty("listBranch").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToList();
            branches.Should().Contain(branch.ToString()).And.OnlyContain(id => id != null);
            d.GetProperty("listBranch").EnumerateArray().Should().NotContain(x => x.GetProperty("name").GetString()!.StartsWith("[DEMO] Bếp"), "chỉ đơn vị loại chi nhánh");
            var users = d.GetProperty("listAssignee").EnumerateArray().ToList();
            users.Should().Contain(x => x.GetProperty("code").GetString() == active.ToString() && x.GetProperty("name").GetString()!.StartsWith("[DEMO] Trần Kinh Doanh"));
            users.Should().NotContain(x => x.GetProperty("code").GetString() == inactive.ToString());
            var catalogs = d.GetProperty("catalogs");
            catalogs.GetProperty("Industry").EnumerateArray().Select(x => x.GetProperty("code").GetString())
                .Should().Equal("HOSPITAL", "FACTORY", "CONSTRUCTION", "SCHOOL", "OFFICE", "OTHER");
            catalogs.GetProperty("Status").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Should().Equal("Đang hoạt động", "Ngừng hoạt động");
        }

        [Fact]
        public async Task Catalog_fields_accept_only_codes_and_keep_old_values_that_are_not_edited()
        {
            var (branch, _, _) = await SeedAsync();
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Ngành", "0355555501", ("Industry", "hospital"), ("CompanySize", "200_500"), ("Source", "TENDER")))).Ok();
            var b = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.LegalName == "[DEMO] UI5 Ngành"));
            b.Industry.Should().Be("HOSPITAL");
            b.CompanySize.Should().Be("200_500");
            b.Source.Should().Be("TENDER");

            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Ngành lạ", "0355555502", ("Industry", "Bệnh viện tư")))).Fail("Ngành \"Bệnh viện tư\" không có trong danh mục");
            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Quy mô lạ", "0355555503", ("CompanySize", "300")))).Fail("Quy mô");
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "INDIVIDUAL"), ("LastName", "[DEMO] Lê"), ("FirstName", "Ba"), ("Address", "[DEMO]"),
                ("PhoneNumber", "0907654321"), ("BranchId", branch), ("Gender", "Nam"))).Fail("Giới tính");
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "INDIVIDUAL"), ("LastName", "[DEMO] Lê"), ("FirstName", "Bốn"), ("Address", "[DEMO]"),
                ("PhoneNumber", "0907654322"), ("BranchId", branch), ("Gender", "FEMALE"), ("Source", "REFERRAL"))).Ok();

            // Dữ liệu cũ ngoài danh mục: sửa trường khác → giữ; đổi sang giá trị lạ khác → lỗi.
            Guid old = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = old, Code = "KHB-UI5OLD", CIF = "KHB-UI5OLD", Name = "[DEMO] UI5 Cũ", LegalName = "[DEMO] UI5 Cũ", DisplayName = "[DEMO] UI5 Cũ",
                    CustomerType = "BUSINESS", Status = "Active", Industry = "Nhà máy may", Source = "Hội chợ", BranchId = branch });
                return Task.CompletedTask;
            });
            (await c.PutFormAsync("/api/Customer/Update", Business(branch, "[DEMO] UI5 Cũ", "0355555504", ("Id", old), ("Industry", "Nhà máy may"), ("Source", "Hội chợ")))).Ok();
            var kept = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Id == old));
            kept.Industry.Should().Be("Nhà máy may");
            kept.Source.Should().Be("Hội chợ");
            (await c.PutFormAsync("/api/Customer/Update", Business(branch, "[DEMO] UI5 Cũ", "0355555504", ("Id", old), ("Industry", "Xưởng")))).Fail("Ngành");
        }

        [Fact]
        public async Task Business_customer_gets_a_primary_contact_and_optional_first_delivery_location_in_the_same_save()
        {
            var (branch, active, _) = await SeedAsync();
            using var c = _factory.CreateAuthorizedClient();
            var ok = await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Điểm giao", "0355555511", ("UserAssignId", active),
                ("FirstLocation.Name", "[DEMO] Cổng chính"), ("FirstLocation.Address", "[DEMO] 1 Lê Lợi"),
                ("FirstLocation.DeliveryWindowFrom", "10:30"), ("FirstLocation.DeliveryWindowTo", "11:15")));
            ok.Ok();
            ok.Message.Should().MatchRegex(@"^Đã tạo khách hàng – mã KHB-\d{5} · 1 liên hệ chính · 1 điểm giao DP-\d+$");
            var b = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.LegalName == "[DEMO] UI5 Điểm giao"));
            b.UserAssignId.Should().Be(active);
            var contact = await _factory.QueryAsync(db => db.CustomerContact.AsNoTracking().SingleAsync(x => x.CustomerId == b.Id));
            contact.IsPrimary.Should().BeTrue();
            contact.FullName.Should().Be("[DEMO] Chị B");
            contact.Phone.Should().Be("0901234567");
            contact.Email.Should().Be("b@demo.vn");
            var loc = await _factory.QueryAsync(db => db.CustomerDeliveryLocation.AsNoTracking().SingleAsync(x => x.CustomerId == b.Id));
            loc.Name.Should().Be("[DEMO] Cổng chính");
            loc.DeliveryWindowFrom.Should().Be(new TimeSpan(10, 30, 0));
            loc.DeliveryWindowTo.Should().Be(new TimeSpan(11, 15, 0));
            loc.Status.Should().Be("Active");

            // Không có khối điểm giao → chỉ liên hệ chính.
            var plain = await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Không điểm giao", "0355555512"));
            plain.Ok();
            plain.Message.Should().EndWith("· 1 liên hệ chính");

            // Cá nhân: không tạo liên hệ chính.
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "INDIVIDUAL"), ("LastName", "[DEMO] Võ"), ("FirstName", "Năm"), ("Address", "[DEMO]"),
                ("PhoneNumber", "0907654330"), ("BranchId", branch))).Message.Should().MatchRegex(@"KHC-\d{5}$");
        }

        [Fact]
        public async Task Invalid_first_location_or_assignee_rejects_the_whole_customer()
        {
            var (branch, _, inactive) = await SeedAsync();
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Giờ sai", "0355555521",
                ("FirstLocation.Name", "[DEMO] Cổng"), ("FirstLocation.Address", "[DEMO] 2 Lê Lợi"),
                ("FirstLocation.DeliveryWindowFrom", "12:00"), ("FirstLocation.DeliveryWindowTo", "11:00")))).Fail("giờ nhận từ phải nhỏ hơn");
            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 Thiếu địa chỉ", "0355555522", ("FirstLocation.Name", "[DEMO] Cổng")))).Fail("thiếu địa chỉ");
            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 NV nghỉ", "0355555523", ("UserAssignId", inactive)))).Fail("nhân viên phụ trách");
            (await c.PostFormAsync("/api/Customer/Insert", Business(branch, "[DEMO] UI5 NV lạ", "0355555524", ("UserAssignId", Guid.NewGuid())))).Fail("nhân viên phụ trách");

            (await _factory.QueryAsync(db => db.Customer.AsNoTracking().AnyAsync(x => x.LegalName!.StartsWith("[DEMO] UI5 Giờ sai") || x.LegalName!.StartsWith("[DEMO] UI5 Thiếu")
                || x.LegalName!.StartsWith("[DEMO] UI5 NV")))).Should().BeFalse("lỗi ở bất kỳ phần nào → không tạo khách");
            (await _factory.QueryAsync(db => db.CustomerContact.AsNoTracking().AnyAsync(x => x.FullName == "[DEMO] Chị B" && !db.Customer.Any(k => k.Id == x.CustomerId))))
                .Should().BeFalse("không có liên hệ mồ côi");
        }
    }
}
