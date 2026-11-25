using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Qc
{
    /// <summary>
    /// UI-6.33 (D-114): sự cố NCC không xóa, Đã xử lý không sửa, Loại trong danh mục, Đã xử lý bắt buộc hướng xử lý chuẩn ("[MÃ] ghi chú") + thống kê;
    /// bộ kiểm tra hệ thống không đổi mã / ngừng / xóa; Tạo phiên bản mới chép tiêu chí, bản cũ hết hiệu lực đúng ngày; bộ tự tạo hạn = hết ca.
    /// </summary>
    public class SupplierIssueChecklistUi633Tests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2038, 1, 20);
        private readonly InMemoryApiFactory _factory;

        public SupplierIssueChecklistUi633Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Day.AddHours(9);
        }

        [Fact]
        public async Task Supplier_issue_rules_and_resolution_summary()
        {
            Guid sup = Guid.NewGuid();
            await _factory.SeedAsync(db => { db.Supplier.Add(new Supplier { Id = sup, Code = "S633", Name = "NCC 633", Status = "Active" }); return Task.CompletedTask; });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/SupplierIssue/Insert", ("SupplierId", sup), ("IssueType", "RAT_TE"), ("Severity", "Medium"), ("Description", "x"))).Fail("không có trong danh mục");
            (await c.PostFormAsync("/api/SupplierIssue/Insert", ("SupplierId", sup), ("IssueType", "LATE"), ("Severity", "High"), ("Description", "Giao trễ 2 giờ"))).Ok();
            var issue = await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.SupplierId == sup));
            (issue.IssueStatus, issue.DueAt).Should().Be(("Open", Day.AddHours(9).AddDays(2)));
            (await c.DeleteResultAsync($"/api/SupplierIssue/Delete?id={issue.Id}")).Fail("không xóa được");

            (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Resolved"), ("Resolution", "Đã đổi"))).Fail("Chọn hướng xử lý");
            var done = (await c.PostFormAsync("/api/SupplierIssue/Transition", ("Id", issue.Id), ("To", "Resolved"), ("ResolutionType", "EXCHANGE"), ("Resolution", "NCC đổi lô mới"))).Ok();
            (done.GetProperty("resolutionTypeName").GetString(), done.GetProperty("resolutionNote").GetString()).Should().Be(("Đổi hàng", "NCC đổi lô mới"));
            (await _factory.QueryAsync(db => db.SupplierIssue.AsNoTracking().SingleAsync(i => i.Id == issue.Id))).Resolution.Should().Be("[EXCHANGE] NCC đổi lô mới");
            (await c.PutFormAsync("/api/SupplierIssue/Update", ("Id", issue.Id), ("SupplierId", sup), ("IssueType", "LATE"), ("Severity", "Low"), ("Description", "sửa"), ("ReportedAt", Day))).Fail("không sửa được");

            var sum = (await c.GetResultAsync($"/api/SupplierIssue/GetSummary?from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}")).Ok().EnumerateArray().Single(r => r.GetProperty("supplierId").GetGuid() == sup);
            sum.GetProperty("byResolution").GetProperty("Đổi hàng").GetInt32().Should().Be(1);
            sum.GetProperty("byType").GetProperty("Giao trễ").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task System_checklist_locked_new_version_and_custom_due_end_of_shift()
        {
            Guid prep = Guid.NewGuid(), custom = Guid.NewGuid(), shift = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "S633", Name = "Ca 633", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                if (!db.FoodSafetyChecklist.Any(x => x.Code == "PREP"))
                {
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = prep, Code = "PREP", Name = "Kiểm tra sơ chế", VersionNo = 1, Status = "Active", EffectiveFrom = Day.AddDays(-30) });
                    db.FoodSafetyChecklistItem.AddRange(
                        new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = prep, ItemCode = "A", ItemName = "Tiêu chí A", SortOrder = 1, IsRequired = true, IsCritical = true },
                        new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = prep, ItemCode = "B", ItemName = "Tiêu chí B", SortOrder = 2, IsRequired = true });
                }
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = custom, Code = "BKT_TU_TAO", Name = "Bộ tự tạo", VersionNo = 1, Status = "Active", EffectiveFrom = Day.AddDays(-30) });
                db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = custom, ItemCode = "X", ItemName = "X", SortOrder = 1, IsRequired = true });
                return Task.CompletedTask;
            });
            prep = await _factory.QueryAsync(db => db.FoodSafetyChecklist.AsNoTracking().Where(x => x.Code == "PREP").OrderBy(x => x.VersionNo).Select(x => x.Id).FirstAsync());
            using var c = _factory.CreateAuthorizedClient();
            (await c.PutFormAsync("/api/FoodSafetyChecklist/Update", ("Id", prep), ("Code", "PREP2"), ("Name", "Kiểm tra sơ chế"), ("Status", "Active"))).Fail("bộ hệ thống");
            (await c.PutFormAsync("/api/FoodSafetyChecklist/Update", ("Id", prep), ("Code", "PREP"), ("Name", "Kiểm tra sơ chế"), ("Status", "Inactive"))).Fail("bộ hệ thống");
            (await c.DeleteResultAsync($"/api/FoodSafetyChecklist/Delete?id={prep}")).Fail("bộ hệ thống");

            (await c.PostFormAsync("/api/FoodSafetyChecklist/NewVersion", ("id", prep), ("effectiveFrom", Day))).Fail("từ ngày mai");
            var v2 = (await c.PostFormAsync("/api/FoodSafetyChecklist/NewVersion", ("id", prep), ("effectiveFrom", Day.AddDays(3)))).Ok();
            Guid v2Id = v2.GetProperty("id").GetGuid();
            v2.GetProperty("versionNo").GetInt32().Should().BeGreaterThan(1);
            (await _factory.QueryAsync(db => db.FoodSafetyChecklistItem.AsNoTracking().CountAsync(i => i.ChecklistId == v2Id && !i.IsDeleted))).Should().Be(
                await _factory.QueryAsync(db => db.FoodSafetyChecklistItem.AsNoTracking().CountAsync(i => i.ChecklistId == prep && !i.IsDeleted)));
            (await _factory.QueryAsync(db => db.FoodSafetyChecklist.AsNoTracking().SingleAsync(x => x.Id == prep))).EffectiveTo.Should().Be(Day.AddDays(2));
            // Ngày áp dụng bản mới → ma trận ngày đó dùng bản mới.
            var board = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetDayBoard?date={Day.AddDays(3):yyyy-MM-dd}")).Ok();
            board.GetProperty("listChecklist").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "PREP").GetProperty("checklistId").GetGuid().Should().Be(v2Id);

            // Bộ tự tạo: hạn = hết ca (14:00).
            var run = (await c.GetResultAsync($"/api/FoodSafetyChecklistResult/GetRun?ChecklistId={custom}&ShiftId={shift}&CheckDate={Day:yyyy-MM-dd}")).Ok();
            run.GetProperty("dueAt").GetDateTime().Should().Be(Day.AddHours(14));
            run.GetProperty("dueSource").GetString().Should().Contain("Trong ca");
        }
    }
}
