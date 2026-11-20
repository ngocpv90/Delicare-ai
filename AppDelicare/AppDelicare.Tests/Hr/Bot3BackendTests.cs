using System.Net.Http.Headers;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Jobs;
using AppDelicare.Model.Model;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Hr
{
    /// <summary>
    /// BOT-3.2 / 3.3 (D-72): API mới cho chức năng bot – đổi xe / tài xế (DeliveryRoute/Reassign), nấu thêm (ProductionBatch/CreateExtra),
    /// nhập file máy chấm công (ImportExcel AttendanceMachine), "tổ tôi" (ShiftAssignment/GetGrid myUnit), báo cáo tuần / tháng (PeriodReportJob.Lines).
    /// </summary>
    public class Bot3BackendTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory = factory;
        private static readonly DateTime Today = DateTime.UtcNow.AddHours(7).Date;

        [Fact]
        public async Task Reassign_vehicle_and_driver_only_before_dispatch()
        {
            Guid route = Guid.NewGuid(), done = Guid.NewGuid(), v1 = Guid.NewGuid(), v2 = Guid.NewGuid(), d1 = Guid.NewGuid(), d2 = Guid.NewGuid(), off = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Vehicle.Add(new Vehicle { Id = v1, Code = "XE-R1", LicensePlate = "51C-111", Status = "Active" });
                db.Vehicle.Add(new Vehicle { Id = v2, Code = "XE-R2", LicensePlate = "51C-222", Status = "Active" });
                db.Employee.Add(new Employee { Id = d1, Code = "NV-R1", FullName = "Tài xế Một", Status = "Active" });
                db.Employee.Add(new Employee { Id = d2, Code = "NV-R2", FullName = "Tài xế Hai", Status = "Active" });
                db.Employee.Add(new Employee { Id = off, Code = "NV-R3", FullName = "Đã nghỉ", Status = "Resigned" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-R1", DeliveryDate = Today.AddDays(1), VehicleId = v1, DriverId = d1, Status = "Planned" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = done, RouteNo = "CX-R2", DeliveryDate = Today, VehicleId = v1, DriverId = d1, Status = "InTransit" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/DeliveryRoute/Reassign", ("Id", route))).Fail("Chưa chọn xe");
            (await c.PostFormAsync("/api/DeliveryRoute/Reassign", ("Id", route), ("DriverId", off))).Fail("tài xế");
            var r = await c.PostFormAsync("/api/DeliveryRoute/Reassign", ("Id", route), ("VehicleId", v2), ("DriverId", d2), ("Note", "Xe 1 hỏng"));
            r.Ok();
            r.Message.Should().Contain("xe và tài xế");
            var saved = await _factory.QueryAsync(db => db.DeliveryRoute.AsNoTracking().SingleAsync(x => x.Id == route));
            saved.VehicleId.Should().Be(v2);
            saved.DriverId.Should().Be(d2);
            (await _factory.QueryAsync(db => db.DeliveryEvent.AsNoTracking().SingleAsync(e => e.DeliveryRouteId == route && e.EventType == "REASSIGNED")))
                .Description.Should().Contain("51C-111 → 51C-222").And.Contain("Tài xế Một → Tài xế Hai").And.Contain("Xe 1 hỏng");
            (await c.PostFormAsync("/api/DeliveryRoute/Reassign", ("Id", done), ("VehicleId", v2))).Fail("đổi xe / tài xế");
        }

        [Fact]
        public async Task Create_extra_batch_on_same_order_with_reason()
        {
            Guid order = Guid.NewGuid(), batch = Guid.NewGuid(), shift = Guid.NewGuid(), dish = Guid.NewGuid(), recipe = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "TRUA", Name = "Trưa" });
                db.ProductionOrder.Add(new ProductionOrder { Id = order, ProductionOrderNumber = "LS-EX-1", DishId = dish, RecipeId = recipe, ShiftId = shift, PlannedQty = 100, Status = "InProgress" });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, BatchNumber = "ME-EX-01", ProductionOrderId = order, DishId = dish, RecipeId = recipe, PlannedQty = 100, PlannedQuantity = 100,
                    StartDate = Today.AddHours(8), EndDate = Today.AddHours(9), Status = "QCPending" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var r = (await c.PostFormAsync("/api/ProductionBatch/CreateExtra", ("Id", batch), ("Quantity", 20m), ("Reason", "Thiếu 20 suất khách A"))).Ok();
            r.GetProperty("batch").GetProperty("status").GetString().Should().Be("Planned");
            r.GetProperty("batch").GetProperty("plannedQuantity").GetDecimal().Should().Be(20);
            r.GetProperty("listWarning").EnumerateArray().Select(x => x.GetString()).Should().Contain(w => w!.Contains("phiếu xuất nguyên liệu"));
            var o = await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(x => x.Id == order));
            o.PlannedQty.Should().Be(120);
            o.Note.Should().Contain("Thiếu 20 suất khách A");
            (await _factory.QueryAsync(db => db.ProductionBatch.CountAsync(x => x.ProductionOrderId == order))).Should().Be(2);
        }

        [Fact]
        public async Task Attendance_machine_file_matches_shift_infers_in_out_and_is_idempotent()
        {
            Guid emp = Guid.NewGuid(), shift = Guid.NewGuid(), asg = Guid.NewGuid();
            string code = "NV-MCC-" + emp.ToString("N")[..4];
            await _factory.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = emp, Code = code, FullName = "Máy chấm", Status = "Active" });
                db.Shift.Add(new Shift { Id = shift, Code = "SANG", Name = "Sáng" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = asg, EmployeeId = emp, ShiftId = shift, WorkDate = Today, PlannedStart = Today.AddHours(6), PlannedEnd = Today.AddHours(14), AssignmentStatus = "Confirmed" });
                return Task.CompletedTask;
            });
            byte[] file;
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("DuLieu");
                ws.Cell(1, 1).Value = "Mã nhân viên"; ws.Cell(1, 2).Value = "Thời điểm"; ws.Cell(1, 3).Value = "Loại"; ws.Cell(1, 4).Value = "Mã máy";
                ws.Cell(2, 1).Value = code; ws.Cell(2, 2).Value = Today.AddHours(6).AddMinutes(2).ToString("yyyy-MM-dd HH:mm"); ws.Cell(2, 4).Value = "MCC-1";
                ws.Cell(3, 1).Value = code; ws.Cell(3, 2).Value = Today.AddHours(14).AddMinutes(5).ToString("dd/MM/yyyy HH:mm");
                ws.Cell(4, 1).Value = "KHONG-CO"; ws.Cell(4, 2).Value = Today.AddHours(6).ToString("yyyy-MM-dd HH:mm");
                ws.Cell(5, 1).Value = code; ws.Cell(5, 2).Value = Today.AddDays(5).ToString("yyyy-MM-dd HH:mm");
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                file = ms.ToArray();
            }
            using var c = _factory.CreateAuthorizedClient();
            var first = (await ImportAsync(c, file)).Ok();
            first.GetProperty("inserted").GetInt32().Should().Be(2);
            first.GetProperty("listError").GetArrayLength().Should().Be(2, "mã NV không có + ngoài ca được phân");
            var events = await _factory.QueryAsync(db => db.AttendanceEvent.AsNoTracking().Where(e => e.EmployeeId == emp).OrderBy(e => e.EventAt).ToListAsync());
            events.Select(e => e.EventType).Should().Equal("CHECK_IN", "CHECK_OUT");
            events.Should().OnlyContain(e => e.SourceChannel == "MACHINE" && e.ShiftAssignmentId == asg);
            (await _factory.QueryAsync(db => db.ShiftAssignment.AsNoTracking().SingleAsync(a => a.Id == asg))).AssignmentStatus.Should().Be("Completed");
            var again = (await ImportAsync(c, file)).Ok();
            again.GetProperty("inserted").GetInt32().Should().Be(0);
            again.GetProperty("skipped").GetInt32().Should().Be(2, "nạp lại không tạo trùng");
            (await c.GetStringAsync("/api/ImportExcel/GetTemplateList")).Should().Contain("\"tableName\":\"AttendanceMachine\"");
        }

        private static async Task<LegacyResult> ImportAsync(HttpClient c, byte[] file)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("AttendanceMachine"), "TableName");
            var content = new ByteArrayContent(file);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            form.Add(content, "File", "mcc.xlsx");
            return await LegacyResult.ReadAsync(await c.PostAsync("/api/ImportExcel/Import", form));
        }

        [Fact]
        public async Task Shift_grid_my_unit_requires_employee_with_unit()
        {
            using var c = _factory.CreateAuthorizedClient();
            // Tài khoản admin test không gắn nhân viên có đơn vị → báo rõ (không trả cả công ty).
            (await c.GetResultAsync($"/api/ShiftAssignment/GetGrid?fromDate={Today:yyyy-MM-dd}&days=1&myUnit=true")).Fail("tổ tôi");
            (await c.GetResultAsync($"/api/ShiftAssignment/GetGrid?fromDate={Today:yyyy-MM-dd}&days=1")).Ok();
        }

        [Fact]
        public void Period_report_lines_average_week_and_compare_target_direction()
        {
            var d = new KpiDashboardModel
            {
                Metrics =
                [
                    new KpiMetricSeriesModel { Definition = new KpiDefinitionModel { MetricName = "Giao đúng giờ", UnitCode = "%", TargetValue = 95, TargetDirection = "HIGHER" },
                        Series = [new KpiSeriesPointModel { Value = 90 }, new KpiSeriesPointModel { Value = 96 }, new KpiSeriesPointModel { Value = null }] },
                    new KpiMetricSeriesModel { Definition = new KpiDefinitionModel { MetricName = "Food cost", UnitCode = "%", TargetValue = 40, TargetDirection = "LOWER" },
                        Series = [new KpiSeriesPointModel { Value = 38 }] },
                ],
            };
            var lines = PeriodReportJob.Lines(d, weekly: true);
            lines.Should().Equal("⚠️ Giao đúng giờ: 93 / mục tiêu 95 %", "✅ Food cost: 38 / mục tiêu 40 %");
            PeriodReportJob.Lines(null, weekly: false).Single().Should().Contain("Chưa có số liệu KPI");
        }
    }
}
