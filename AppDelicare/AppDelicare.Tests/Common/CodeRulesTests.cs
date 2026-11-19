using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// MA-1.7 (D-63 k, BUSINESS_RULES §11.3) – chốt chặn quy tắc mã toàn hệ thống:
    /// (1) mọi cột mã (thuộc tính chuỗi / số tên kết thúc Code / No / Number của mọi entity) phải được xếp nhóm G1 / G2 / G3 – thiếu → đỏ;
    /// (2) tiền tố G1 duy nhất, không tiền tố nào là phần đầu của tiền tố khác; (3) mỗi cột chỉ thuộc 1 nhóm.
    /// </summary>
    public class CodeRulesTests
    {
        private static readonly string[] CodeSuffixes = ["Code", "No", "Number"];

        private static IEnumerable<(Type Entity, PropertyInfo Property)> CodeColumns()
            => typeof(BaseEntity).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == typeof(BaseEntity).Namespace && t.GetCustomAttribute<TableAttribute>() != null)
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => (t, p)))
                .Where(x => CodeSuffixes.Any(s => x.p.Name.EndsWith(s, StringComparison.Ordinal))
                    && (x.p.PropertyType == typeof(string) || Nullable.GetUnderlyingType(x.p.PropertyType) is { IsPrimitive: true } || x.p.PropertyType.IsPrimitive));

        [Fact]
        public void Every_code_column_of_every_entity_is_classified_G1_G2_or_G3()
        {
            var missing = CodeColumns().Where(x => CodeRules.Find(x.Entity, x.Property.Name) == null)
                .Select(x => $"{x.Entity.Name}.{x.Property.Name}").ToList();
            missing.Should().BeEmpty("D-63 k: thêm bảng / cột mã phải xếp nhóm G1 / G2 / G3 trong CodeRules (BUSINESS_RULES §11.3) và đăng ký tiền tố G1 không trùng");
        }

        [Fact]
        public void Each_column_is_in_exactly_one_group_and_points_to_a_real_property()
        {
            CodeRules.All.GroupBy(x => x.Entity + "." + x.Column).Where(g => g.Select(x => x.Group).Distinct().Count() > 1).Select(g => g.Key)
                .Should().BeEmpty("mỗi cột thuộc đúng 1 nhóm");
            var entities = typeof(BaseEntity).Assembly.GetTypes().Where(t => t.Namespace == typeof(BaseEntity).Namespace).ToDictionary(t => t.Name);
            CodeRules.All.Where(x => !entities.TryGetValue(x.Entity, out var t) || t.GetProperty(x.Column) == null).Select(x => x.Entity + "." + x.Column)
                .Should().BeEmpty("bảng phân nhóm không trỏ tới cột không có");
        }

        [Fact]
        public void G1_prefixes_are_unique_and_none_is_the_start_of_another()
        {
            var prefixes = DocumentNumberService.Kinds.Select(k => k.Prefix).ToList();
            prefixes.Should().OnlyHaveUniqueItems();
            prefixes.Should().OnlyContain(p => System.Text.RegularExpressions.Regex.IsMatch(p, "^[A-Z]{2,3}$"));
            var clashes = prefixes.SelectMany(a => prefixes.Where(b => a != b && b.StartsWith(a, StringComparison.Ordinal)).Select(b => a + " ⊂ " + b)).ToList();
            clashes.Should().BeEmpty("BUSINESS_RULES §11 luật 2: không tiền tố nào là phần đầu của tiền tố khác");
        }

        [Theory]
        [InlineData("ca1", "CA1")]
        [InlineData(" Kho-Lạnh_01 ", "KHO-LANH_01")]
        public void G2_user_code_is_normalized_to_upper_case_without_accents(string input, string expected)
            => CodeRules.NormalizeUserCode(input).Code.Should().Be(expected);

        [Theory]
        [InlineData("")]
        [InlineData("A B")]
        [InlineData("KHO/1")]
        public void G2_user_code_rejects_blank_spaces_and_other_characters(string input)
            => CodeRules.NormalizeUserCode(input).Error.Should().NotBeNullOrEmpty();
    }

    /// <summary>MA-1.2 / MA-1.3 (D-63): Insert của bảng G1 bỏ qua mã gửi lên và sinh mã; Update không đổi mã; khách hàng theo loại.</summary>
    public class CodeRulesApiTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public CodeRulesApiTests(InMemoryApiFactory factory) => _factory = factory;

        private async Task<Guid> BranchAsync()
        {
            Guid branch = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = branch, CompanyId = Guid.NewGuid(), Code = "CN-" + branch.ToString("N")[..6].ToUpperInvariant(), Name = "[DEMO] Chi nhánh" });
                return Task.CompletedTask;
            });
            return branch;
        }

        private static (string, object?)[] Business(Guid branch, string name, string tax) =>
        [
            ("CustomerType", "BUSINESS"), ("LegalName", name), ("DisplayName", name), ("TaxCode", tax), ("LegalRepresentativeName", "[DEMO] Ông A"),
            ("ContactPersonName", "[DEMO] Chị B"), ("ContactPersonPhone", "0901234567"), ("Address", "[DEMO] Q1"), ("PhoneNumber", "+84281234567"), ("BranchId", branch),
        ];

        [Fact]
        public async Task GetCodeRules_returns_groups_for_the_ui()
        {
            using var c = _factory.CreateAuthorizedClient();
            var list = (await c.GetResultAsync("/api/Common/GetCodeRules?entity=Customer")).ListData.EnumerateArray().ToList();
            list.Should().Contain(x => x.GetProperty("column").GetString() == "Code" && x.GetProperty("group").GetString() == "G1" && x.GetProperty("prefix").GetString() == "KHB");
            list.Should().Contain(x => x.GetProperty("column").GetString() == "Code" && x.GetProperty("prefix").GetString() == "KHC");
            list.Should().Contain(x => x.GetProperty("column").GetString() == "TaxCode" && x.GetProperty("group").GetString() == "G3");
            var shift = (await c.GetResultAsync("/api/Common/GetCodeRules?entity=Shift")).ListData.EnumerateArray().ToList();
            shift.Should().ContainSingle(x => x.GetProperty("group").GetString() == "G2");
        }

        [Fact]
        public async Task Business_and_individual_customers_get_server_codes_cif_equals_code_and_required_fields_by_type()
        {
            Guid branch = await BranchAsync();
            using var c = _factory.CreateAuthorizedClient();
            // Code / CIF gửi lên bị bỏ qua; thông báo thành công kèm mã.
            var ok = await c.PostFormAsync("/api/Customer/Insert", [.. Business(branch, "[DEMO] Công ty Mã 1", "0311111111"), ("Code", "TU-GO"), ("CIF", "TU-GO")]);
            ok.Ok();
            ok.Message.Should().MatchRegex(@"khách hàng – mã KHB-\d{5}$");
            var b = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.LegalName == "[DEMO] Công ty Mã 1"));
            b.Code.Should().MatchRegex(@"^KHB-\d{5}$").And.NotBe("TU-GO");
            b.CIF.Should().Be(b.Code);
            b.Name.Should().Be("[DEMO] Công ty Mã 1");
            b.BusinessRegistrationNumber.Should().Be("0311111111", "số ĐKKD trống = MST");

            // Thiếu trường bắt buộc / sai định dạng → lỗi tiếng Việt rõ trường.
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "BUSINESS"), ("LegalName", "[DEMO] Thiếu"), ("BranchId", branch))).Fail("Mã số thuế");
            (await c.PostFormAsync("/api/Customer/Insert", [.. Business(branch, "[DEMO] Sai MST", "12345")])).Fail("Mã số thuế 12345");
            (await c.PostFormAsync("/api/Customer/Insert", [.. Business(branch, "[DEMO] Trùng MST", "0311111111")])).Fail("đã có");
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "HOSPITAL"), ("LegalName", "[DEMO] Loại lạ"))).Fail("Loại khách hàng");
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "INDIVIDUAL"), ("FirstName", "An"), ("Address", "[DEMO]"), ("PhoneNumber", "0901234567"),
                ("BranchId", branch))).Fail("Họ");

            // Cá nhân: KHC-, Name = Họ + Tên, trường doanh nghiệp bị xóa trống, SĐT gọn.
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "individual"), ("LastName", "[DEMO] Nguyễn"), ("FirstName", "An"), ("Address", "[DEMO] Q3"),
                ("PhoneNumber", "0901.234.568"), ("BranchId", branch), ("IdentityNumber", "079123456789"), ("LegalName", "[DEMO] Không thuộc loại"))).Ok();
            var p = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.IdentityNumber == "079123456789"));
            p.Code.Should().MatchRegex(@"^KHC-\d{5}$");
            p.CIF.Should().Be(p.Code);
            p.Name.Should().Be("[DEMO] Nguyễn An");
            p.DisplayName.Should().Be(p.Name);
            p.LegalName.Should().BeNull("trường không thuộc loại bị xóa trống");
            p.PhoneNumber.Should().Be("0901234568");

            // Update: Code / CIF không đổi.
            (await c.PutFormAsync("/api/Customer/Update", [.. Business(branch, "[DEMO] Công ty Mã 1", "0311111111"), ("Id", b.Id), ("Code", "DOI-MA"), ("CIF", "DOI-MA")])).Ok();
            (await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Id == b.Id))).Code.Should().Be(b.Code);

            // Đổi loại: chưa có hợp đồng / đơn → mã mới theo loại mới; đã có đơn → lỗi.
            (await c.PutFormAsync("/api/Customer/Update", [.. Business(branch, "[DEMO] Hộ KD An", "0322222222"), ("Id", p.Id)])).Ok();
            var changed = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Id == p.Id));
            changed.Code.Should().MatchRegex(@"^KHB-\d{5}$").And.NotBe(p.Code);
            changed.CIF.Should().Be(changed.Code);
            await _factory.SeedAsync(db =>
            {
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = b.Id, OrderNumber = "DH-TEST-MA1", MealDate = DateTime.Today, DeliveryDate = DateTime.Today });
                return Task.CompletedTask;
            });
            (await c.PutFormAsync("/api/Customer/Update", ("Id", b.Id), ("CustomerType", "INDIVIDUAL"), ("LastName", "[DEMO] X"), ("FirstName", "Y"), ("Address", "[DEMO]"),
                ("PhoneNumber", "0901234569"), ("BranchId", branch))).Fail("không đổi loại");
        }

        [Fact]
        public async Task G2_user_code_is_normalized_rejected_when_invalid_and_locked_once_used()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Shift/Insert", ("Code", "ca chiều"), ("Name", "[DEMO] Ca lỗi"), ("StartTime", "13:00:00"), ("EndTime", "17:00:00"))).Fail("chữ hoa không dấu");
            (await c.PostFormAsync("/api/Shift/Insert", ("Code", "cạ-g2"), ("Name", "[DEMO] Ca G2"), ("StartTime", "13:00:00"), ("EndTime", "17:00:00"))).Ok();
            var shift = await _factory.QueryAsync(db => db.Shift.AsNoTracking().SingleAsync(x => x.Name == "[DEMO] Ca G2"));
            shift.Code.Should().Be("CA-G2", "G2: chuyển chữ hoa không dấu");

            // Chưa dùng → sửa mã được; đã có phân ca tham chiếu → khóa sửa mã (vẫn sửa tên được).
            (await c.PutFormAsync("/api/Shift/Update", ("Id", shift.Id), ("Code", "ca-g2b"), ("Name", "[DEMO] Ca G2"), ("StartTime", "13:00:00"), ("EndTime", "17:00:00"))).Ok();
            await _factory.SeedAsync(db =>
            {
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shift.Id, EmployeeId = Guid.NewGuid(), WorkDate = DateTime.Today.AddDays(30) });
                return Task.CompletedTask;
            });
            (await c.PutFormAsync("/api/Shift/Update", ("Id", shift.Id), ("Code", "CA-KHAC"), ("Name", "[DEMO] Ca G2"), ("StartTime", "13:00:00"), ("EndTime", "17:00:00"))).Fail("đã được dùng");
            (await c.PutFormAsync("/api/Shift/Update", ("Id", shift.Id), ("Code", "CA-G2B"), ("Name", "[DEMO] Ca G2 đổi tên"), ("StartTime", "13:00:00"), ("EndTime", "17:00:00"))).Ok();
        }

        [Fact]
        public async Task Catalog_insert_ignores_sent_code_and_update_keeps_it()
        {
            using var c = _factory.CreateAuthorizedClient();
            var ok = await c.PostFormAsync("/api/Warehouse/Insert", ("Code", "KHO-TU-GO"), ("Name", "[DEMO] Kho MA-1"), ("WarehouseType", "KITCHEN"), ("Status", "Active"));
            ok.Ok();
            ok.Message.Should().MatchRegex(@"kho – mã KO-\d{5}$");
            var wh = await _factory.QueryAsync(db => db.Warehouse.AsNoTracking().SingleAsync(x => x.Name == "[DEMO] Kho MA-1"));
            wh.Code.Should().MatchRegex(@"^KO-\d{5}$");
            (await c.PutFormAsync("/api/Warehouse/Update", ("Id", wh.Id), ("Code", "DOI"), ("Name", "[DEMO] Kho MA-1"), ("WarehouseType", "KITCHEN"), ("Status", "Active"))).Ok();
            (await _factory.QueryAsync(db => db.Warehouse.AsNoTracking().SingleAsync(x => x.Id == wh.Id))).Code.Should().Be(wh.Code);

            (await c.PostFormAsync("/api/Supplier/Insert", ("Code", "X"), ("Name", "[DEMO] NCC MA-1"), ("Status", "Active"))).Ok();
            (await _factory.QueryAsync(db => db.Supplier.AsNoTracking().SingleAsync(x => x.Name == "[DEMO] NCC MA-1"))).Code.Should().MatchRegex(@"^NC-\d{5}$");
            (await c.PostFormAsync("/api/Employee/Insert", ("Code", "X"), ("FullName", "[DEMO] NV MA-1"), ("Status", "Active"))).Ok();
            var nv = await _factory.QueryAsync(db => db.Employee.AsNoTracking().SingleAsync(x => x.FullName == "[DEMO] NV MA-1"));
            nv.Code.Should().MatchRegex(@"^NV-\d{5}$");
            nv.EmployeeCode.Should().Be(nv.Code);
        }
    }
}
