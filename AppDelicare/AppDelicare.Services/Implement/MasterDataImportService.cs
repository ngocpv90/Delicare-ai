using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T49 – nạp dữ liệu danh mục ban đầu từ Excel (khung cũ: service + controller ImportExcel).
    /// <list type="bullet">
    /// <item>File mẫu: mỗi danh mục 1 mẫu, cột tiếng Việt (GetTemplate). Nhận diện cột theo tiêu đề, không theo vị trí.</item>
    /// <item>File thật: "Rule Menu.xlsx" (sheet Ngân hàng món, Chi tiết món và hao hụt, giá), "Tool_Tong hop bao gia" (sheet Summary) – cột cố định.</item>
    /// <item>Idempotent theo mã (món / nhóm theo tên). Dòng lỗi bỏ qua và báo số dòng + lý do; dòng hợp lệ ghi trong 1 transaction.</item>
    /// </list>
    /// Không ghi file lên đĩa server (đọc từ stream) – file thật chứa giá mua.
    /// </summary>
    public partial class MasterDataImportService(IUnitOfWork unitOfWork, IRecipeService recipeService, IOptions<BusinessRulesOptions> rules,
        IDocumentNumberService documentNumberService, IOptions<ReferenceListsOptions> referenceLists) : IMasterDataImportService
    {
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;

        /// <summary>
        /// GAP-REF-01 (anh chốt 2026-11-22): cột mã danh mục (Loại NCC, Kiểu bảo quản, Loại kho) của file nhập phải có trong ReferenceLists – nhận mã hoặc nhãn → mã.
        /// Giá trị cũ ngoài danh mục giữ được khi bản ghi cùng mã đang có đúng giá trị đó. Kiểm TRƯỚC khi thêm bản ghi (dòng lỗi không để lại bản ghi dở).
        /// </summary>
        private async Task<(string? Code, string? Error)> RefAsync<T>(string column, string? value, System.Linq.Expressions.Expression<Func<T, bool>> sameRecord,
            System.Linq.Expressions.Expression<Func<T, string?>> stored) where T : class
        {
            var (code, error) = ReferenceCatalogs.CheckColumn(_referenceLists, column, value);
            if (error == null)
            {
                return (code, null);
            }
            string? old = await _unitOfWork.DbContext.Set<T>().AsNoTracking().Where(sameRecord).Select(stored).FirstOrDefaultAsync();
            return ReferenceCatalogs.CheckColumn(_referenceLists, column, value, old);
        }
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;

        /// <summary>MA-1.3 (D-63): ô mã trống → máy chủ sinh mã danh mục (G1); có mã → khớp theo mã như cũ (dữ liệu đã có giữ mã).</summary>
        private async Task<string> CodeOrNewAsync(string? code, DocumentKind kind)
            => string.IsNullOrWhiteSpace(code) ? await _documentNumberService.NextCodeAsync(kind) : code.Trim();

        private const string GeneratedCodeNote = "Trống = máy chủ tự sinh mã (D-63); có mã = cập nhật bản ghi có mã đó";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IRecipeService _recipeService = recipeService;
        private readonly BusinessRulesOptions _rules = rules.Value;

        private const string Active = "Active";

        // ============================================================ Danh mục file nhập
        private sealed record Col(string Header, bool Required = false, string? Note = null, string? Sample = null);

        private sealed record Kind(string TableName, string Title, string Screen, Col[] Columns, string? RealSheet = null);

        private static readonly Kind[] Kinds =
        [
            new("Customer", "Khách hàng", "crm-customer",
            [
                new("Mã khách hàng", false, GeneratedCodeNote, "[DEMO]-KH01"), new("Tên khách hàng", true, null, "[DEMO] Trường Ánh Dương"),
                new("Loại khách", true, "BUSINESS (doanh nghiệp) / INDIVIDUAL (cá nhân); giá trị khác (ngành) → Doanh nghiệp + ghi vào Ngành", "BUSINESS"), new("Tên hiển thị", false, "Trống = tên khách hàng"),
                new("CIF", false, "Bỏ qua – CIF = mã khách hàng (D-63)"), new("Mã số thuế"), new("Điện thoại"), new("Email"), new("Địa chỉ"),
                new("Trạng thái", false, "Active / Inactive, trống = Active"),
            ]),
            new("CustomerDeliveryLocation", "Điểm giao hàng", "crm-location",
            [
                new("Mã khách hàng", true, null, "[DEMO]-KH01"), new("Mã điểm giao", false, GeneratedCodeNote, "[DEMO]-DG01"), new("Tên điểm giao", true, null, "[DEMO] Cổng A"),
                new("Địa chỉ"), new("Người nhận"), new("SĐT người nhận"), new("Giờ nhận từ", false, "HH:mm", "10:30"), new("Giờ nhận đến", false, "HH:mm", "11:15"),
                new("Vĩ độ"), new("Kinh độ"), new("Hướng dẫn giao"),
            ]),
            new("Supplier", "Nhà cung cấp", "scm-supplier",
            [
                new("Mã NCC", false, GeneratedCodeNote, "[DEMO]-NCC01"), new("Tên NCC", true, null, "[DEMO] NCC rau sạch"), new("Loại NCC", false, "MetaData/GetReferenceLists?name=SupplierType", "VEGETABLE"),
                new("Mã số thuế"), new("Điện thoại"), new("Email"), new("Địa chỉ"), new("Người liên hệ"), new("SĐT liên hệ"),
            ]),
            new("UnitMeasure", "Đơn vị tính", "scm-uom",
            [
                new("Mã ĐVT", true, null, "[DEMO]-KG"), new("Tên ĐVT", true, null, "[DEMO] kg"), new("Ký hiệu", false, null, "kg"), new("Số chữ số thập phân", false, null, "3"),
            ]),
            new("Ingredient", "Nguyên liệu", "scm-ingredient",
            [
                new("Mã nguyên liệu", false, GeneratedCodeNote, "[DEMO]-NL01"), new("Tên nguyên liệu", true, null, "[DEMO] Thịt ba chỉ"),
                new("Nhóm nguyên liệu", false, "Tên nhóm; chưa có thì tạo mới", "[DEMO] Thịt"), new("ĐVT", false, "Mã hoặc tên ĐVT; chưa có thì tạo mới", "kg"),
                new("Kiểu bảo quản", false, "MetaData/GetReferenceLists?name=StorageType", "CHILLED"), new("Tỷ lệ thu hồi (%)", false, "(0, 100]"),
                new("Hao hụt chuẩn (%)", false, "[0, 100)", "5"), new("Quản lý hạn dùng", false, "Có / Không", "Có"), new("Số ngày hạn dùng", false, "Bắt buộc > 0 nếu quản lý hạn", "3"),
                new("Tồn tối thiểu"),
            ]),
            new("Warehouse", "Kho", "scm-warehouse",
            [
                new("Mã kho", false, GeneratedCodeNote, "[DEMO]-KHO01"), new("Tên kho", true, null, "[DEMO] Kho lạnh"), new("Loại kho", false, "MetaData/GetReferenceLists?name=WarehouseType", "COLD"),
                new("Địa chỉ"), new("Mã đơn vị", false, "OrganizationUnit.Code"),
            ]),
            new("Employee", "Nhân viên", "adm-employee",
            [
                new("Mã nhân viên", false, GeneratedCodeNote, "[DEMO]-NV01"), new("Họ tên", true, null, "[DEMO] Nguyễn Văn A"), new("Mã đơn vị", false, "OrganizationUnit.Code"),
                new("Mã chức vụ", false, "Position.Code"), new("Điện thoại"), new("Email"), new("Ngày vào làm", false, "yyyy-MM-dd"),
            ]),
            new("Vehicle", "Phương tiện", "log-vehicle",
            [
                new("Mã xe", false, GeneratedCodeNote, "[DEMO]-XE01"), new("Biển số", true, null, "29C-000.00"), new("Loại xe", false, null, "Xe tải lạnh"), new("Tải trọng (kg)", false, null, "1500"),
            ]),
            new("Dish", "Món ăn", "menu-dish",
            [
                new("Mã món", false, GeneratedCodeNote, "[DEMO]-MON01"), new("Tên món", true, null, "[DEMO] Thịt kho trứng"), new("Nhóm món", false, "Tên nhóm; chưa có thì tạo mới", "[DEMO] Món chính"),
                new("Định lượng chuẩn (g/suất)"), new("Mô tả"),
            ]),
            new("RecipeLine", "Định mức công thức (dòng định lượng)", "menu-recipe",
            [
                new("Mã món", true, null, "[DEMO]-MON01"), new("Mã nguyên liệu", true, null, "[DEMO]-NL01"),
                new("Định lượng", true, "Cho số suất chuẩn của công thức (mặc định 100 suất)", "12"), new("ĐVT", true, "Mã hoặc tên ĐVT", "kg"),
                new("% hao hụt sơ chế", false, "Ghi % (5) hoặc tỷ lệ (0,05)"), new("% hao hụt chế biến", false, "Ghi % hoặc tỷ lệ"), new("Ghi chú sơ chế"),
            ]),
            new("SupplierMaterial", "Bảng giá NCC", "scm-supplier-material",
            [
                new("Mã NCC", true, null, "[DEMO]-NCC01"), new("Mã nguyên liệu", true, null, "[DEMO]-NL01"), new("Đơn giá (chưa VAT)", true, null, "125000"),
                new("ĐVT mua", false, "Mã hoặc tên ĐVT", "kg"), new("Hiệu lực từ", false, "yyyy-MM-dd"), new("Hiệu lực đến", false, "yyyy-MM-dd"),
                new("Ưu tiên", false, "Có / Không – mỗi nguyên liệu 1 NCC ưu tiên", "Có"), new("Số lượng đặt tối thiểu"),
            ]),
            // BOT-3.3 (D-72): dữ liệu máy chấm công → AttendanceEvent (hình thức Máy chấm công), khớp ca được phân; nạp lại không trùng.
            new("AttendanceMachine", "Dữ liệu máy chấm công", "hr-attendance",
            [
                new("Mã nhân viên", true, "Mã NV trên hệ thống", "[DEMO]-NV01"), new("Thời điểm", true, "yyyy-MM-dd HH:mm hoặc dd/MM/yyyy HH:mm", "2026-11-20 06:02"),
                new("Loại", false, "Vào / Ra – trống = lần đầu của ca là Vào, sau đó là Ra", "Vào"), new("Mã máy", false, "Ghi chú máy chấm công", "MCC-BEP1"),
            ]),
            // ---- File vận hành thật (BUSINESS_DOCS.md) – cột cố định, không có file mẫu
            new("RuleMenu_DishBank", "Rule Menu.xlsx – Ngân hàng món", "menu-dish",
                [new("Mỗi cột = 1 nhóm món (tiêu đề), mỗi ô = 1 món", true)], "Ngân hàng món"),
            new("RuleMenu_DishDetail", "Rule Menu.xlsx – Chi tiết món và hao hụt, giá", "scm-ingredient",
                [new("B Nhóm, D Tên món (khối món); L Mã, M Thành phần nguyên liệu, N ĐVT, P % hao hụt sơ chế (khối nguyên liệu)", true)], "Chi tiết món và hao hụt, giá"),
            new("BaoGia_Summary", "Tool_Tong hop bao gia – sheet Summary", "scm-supplier-material",
                [new("B Mã, C Tên, D Đơn vị tính chính, E Đơn giá (chưa VAT), I NCC, J Ma_NCC", true)], "Summary"),
        ];

        private static Kind? Find(string? tableName) => Kinds.FirstOrDefault(k => k.TableName.Equals(tableName ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        public string? ScreenOf(string? tableName) => Find(tableName)?.Screen;

        public IResults<ImportTemplateModel> GetTemplateList()
        {
            var list = Kinds.Select(k => new ImportTemplateModel
            {
                TableName = k.TableName, Title = k.Title, Screen = k.Screen, HasTemplate = k.RealSheet == null, SheetName = k.RealSheet,
                ListColumn = k.Columns.Select(c => new ImportColumnModel { Header = c.Header, Required = c.Required, Note = c.Note }).ToList(),
            }).ToList();
            return new Results<ImportTemplateModel>
            {
                ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                ListData = list,
                TotalRecords = list.Count
            };
        }

        public byte[]? BuildTemplate(string? tableName)
        {
            var kind = Find(tableName);
            if (kind == null || kind.RealSheet != null)
            {
                return null;
            }
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("DuLieu");
            for (int i = 0; i < kind.Columns.Length; i++)
            {
                var c = kind.Columns[i];
                var head = ws.Cell(1, i + 1);
                head.Value = c.Header + (c.Required ? " *" : string.Empty);
                head.Style.Font.Bold = true;
                head.Style.Fill.BackgroundColor = c.Required ? XLColor.FromHtml("#F07825") : XLColor.FromHtml("#91AF42");
                head.Style.Font.FontColor = XLColor.White;
                if (c.Note != null)
                {
                    head.CreateComment().AddText(c.Note);
                }
                ws.Cell(2, i + 1).Value = c.Sample ?? string.Empty;
            }
            ws.Columns().AdjustToContents();
            var guide = workbook.Worksheets.Add("HuongDan");
            guide.Cell(1, 1).Value = $"Mẫu nhập: {kind.Title}. Cột có dấu * là bắt buộc. Dòng 2 là ví dụ [DEMO] – xóa trước khi nhập dữ liệu thật.";
            guide.Cell(2, 1).Value = "Nhập lại cùng mã sẽ cập nhật, không tạo trùng. Dòng lỗi được bỏ qua và báo số dòng + lý do.";
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        // ============================================================ Nhập
        public async Task<IResult<ImportResultModel>> Import(string? tableName, Stream file)
        {
            var kind = Find(tableName);
            if (kind == null)
            {
                return Result<ImportResultModel>.Fail(string.Format(ConstantHelper.Message.NotExist, "Loại file nhập " + tableName));
            }
            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(file);
            }
            catch (Exception ex)
            {
                Log.Warning("Import {Table}: không đọc được file Excel – {Error}", kind.TableName, ex.Message);
                return Result<ImportResultModel>.Fail(ConstantHelper.Message.FileCorrectFormat);
            }
            using (workbook)
            using (var tran = await _unitOfWork.OpenTransaction())
            {
                try
                {
                    var result = new ImportResultModel { TableName = kind.TableName };
                    var ctx = new Ctx(result);
                    string? error = kind.RealSheet == null ? await ImportTemplateAsync(kind, workbook, ctx) : await ImportRealAsync(kind, workbook, ctx);
                    if (error != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<ImportResultModel>.Fail(error, result);
                    }
                    await _unitOfWork.DbContext.SaveChangesAsync(ConstantHelper.TypeSave.Update);
                    _unitOfWork.CommitTransaction();
                    Log.Information("Import {Table}: {Total} dòng, thêm {Inserted}, cập nhật {Updated}, bỏ qua {Skipped}, lỗi {Errors}",
                        kind.TableName, result.TotalRows, result.Inserted, result.Updated, result.Skipped, result.ListError.Count);
                    return Result<ImportResultModel>.Success(result, result.ListError.Count == 0
                        ? ConstantHelper.Message.ImportSuccess
                        : $"{ConstantHelper.Message.ImportSuccess}: {result.Inserted + result.Updated} dòng; {result.ListError.Count} dòng lỗi (xem listError).");
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " Import: ", ex.ToString()));
                    _unitOfWork.RollbackTransaction();
                    throw;
                }
            }
        }

        /// <summary>Trạng thái 1 lần nhập: kết quả + bộ nhớ tạm các bản ghi vừa thêm (để dòng sau dùng lại).</summary>
        private sealed class Ctx(ImportResultModel result)
        {
            public ImportResultModel Result { get; } = result;
            public void Error(int row, string message) => Result.ListError.Add(new ImportRowError { Row = row, Message = message });
            public Dictionary<string, UnitMeasure> Units { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, Ingredient> Ingredients { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, Supplier> Suppliers { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, DishCategory> DishCategories { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, IngredientCategory> IngredientCategories { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, Dish> Dishes { get; } = new(StringComparer.OrdinalIgnoreCase);
            /// <summary>Máy chấm công: khóa lần chấm đã ghi trong file này + ca đã có lượt Vào (xác định Vào / Ra khi cột Loại trống).</summary>
            public HashSet<string> AttendanceKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<Guid> CheckedInAssignments { get; } = [];
            public void Count(bool isNew, bool changed)
            {
                if (isNew) Result.Inserted++;
                else if (changed) Result.Updated++;
                else Result.Skipped++;
            }
        }

        /// <summary>Một dòng của sheet mẫu: đọc theo tiêu đề cột.</summary>
        private sealed class TemplateRow(int number, Dictionary<string, string> values)
        {
            public int Number { get; } = number;
            public string? this[string header] => values.TryGetValue(Norm(header), out var v) && v.Length > 0 ? v : null;
        }

        private async Task<string?> ImportTemplateAsync(Kind kind, XLWorkbook workbook, Ctx ctx)
        {
            var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("DuLieu", StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheet(1);
            var required = kind.Columns.Where(c => c.Required).Select(c => Norm(c.Header)).ToList();
            // Dòng tiêu đề: dòng đầu (trong 10 dòng) chứa đủ các cột bắt buộc.
            IXLRow? header = ws.RowsUsed().Take(10).FirstOrDefault(r => required.All(h => r.CellsUsed().Any(c => Norm(CellText(c)) == h)));
            if (header == null)
            {
                return $"Không thấy dòng tiêu đề có đủ cột bắt buộc: {string.Join(", ", kind.Columns.Where(c => c.Required).Select(c => c.Header))}. Tải mẫu: ImportExcel/GetTemplate?tableName={kind.TableName}";
            }
            var map = header.CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => Norm(CellText(c)));
            var rows = new List<TemplateRow>();
            foreach (var r in ws.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var values = map.ToDictionary(m => m.Value, m => CellText(r.Cell(m.Key)).Trim());
                if (values.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }
                rows.Add(new TemplateRow(r.RowNumber(), values));
            }
            ctx.Result.TotalRows = rows.Count;
            foreach (var row in rows)
            {
                var missing = kind.Columns.Where(c => c.Required && row[c.Header] == null).Select(c => c.Header).ToList();
                if (missing.Count != 0)
                {
                    ctx.Error(row.Number, "Thiếu " + string.Join(", ", missing));
                    continue;
                }
                string? err = kind.TableName switch
                {
                    "Customer" => await CustomerAsync(row, ctx),
                    "CustomerDeliveryLocation" => await DeliveryLocationAsync(row, ctx),
                    "Supplier" => await SupplierAsync(row, ctx),
                    "UnitMeasure" => await UnitMeasureAsync(row, ctx),
                    "Ingredient" => await IngredientAsync(row, ctx),
                    "Warehouse" => await WarehouseAsync(row, ctx),
                    "Employee" => await EmployeeAsync(row, ctx),
                    "Vehicle" => await VehicleAsync(row, ctx),
                    "Dish" => await DishAsync(row, ctx),
                    "RecipeLine" => await RecipeLineAsync(row, ctx),
                    "SupplierMaterial" => await SupplierMaterialAsync(row, ctx),
                    "AttendanceMachine" => await AttendanceMachineAsync(row, ctx),
                    _ => "Loại file chưa hỗ trợ.",
                };
                if (err != null)
                {
                    ctx.Error(row.Number, err);
                }
            }
            return null;
        }

        // ------------------------------------------------------------ từng danh mục (mẫu)
        private async Task<string?> CustomerAsync(TemplateRow r, Ctx ctx)
        {
            // D-63 f: loại chỉ BUSINESS / INDIVIDUAL; giá trị cũ (HOSPITAL, SCHOOL…) là ngành → Doanh nghiệp + Industry.
            string? type = CustomerTypes.Normalize(r["Loại khách"]);
            string? industry = type == null ? r["Loại khách"] : null;
            type ??= CustomerTypes.Business;
            string code = await CodeOrNewAsync(r["Mã khách hàng"],
                type == CustomerTypes.Individual ? DocumentKind.CustomerIndividual : DocumentKind.CustomerBusiness);
            var e = await FindAsync<Customer>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.CustomerRepository, new Customer { Id = Guid.NewGuid(), Code = code });
            bool changed = Set(e, x => x.Name, r["Tên khách hàng"]) | Set(e, x => x.CustomerType, type) | Set(e, x => x.DisplayName, r["Tên hiển thị"] ?? r["Tên khách hàng"])
                | Set(e, x => x.Industry, industry ?? e.Industry) | Set(e, x => x.LegalName, type == CustomerTypes.Business ? r["Tên khách hàng"] : e.LegalName)
                | Set(e, x => x.CIF, e.CIF ?? code) | Set(e, x => x.TaxCode, r["Mã số thuế"]) | Set(e, x => x.PhoneNumber, r["Điện thoại"]) | Set(e, x => x.Email, r["Email"])
                | Set(e, x => x.Address, r["Địa chỉ"]) | Set(e, x => x.Status, r["Trạng thái"] ?? e.Status ?? Active);
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> DeliveryLocationAsync(TemplateRow r, Ctx ctx)
        {
            var customer = await FindAsync<Customer>(x => x.Code == r["Mã khách hàng"] && !x.IsDeleted);
            if (customer == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Khách hàng " + r["Mã khách hàng"]);
            }
            if (!TryTime(r["Giờ nhận từ"], out var from) || !TryTime(r["Giờ nhận đến"], out var to))
            {
                return "Giờ nhận phải dạng HH:mm.";
            }
            if (from != null && to != null && from >= to)
            {
                return ConstantHelper.Message.DeliveryWindowInvalid;
            }
            if (!TryDecimal(r["Vĩ độ"], out var lat) || lat is < -90 or > 90)
            {
                return ConstantHelper.Message.LatitudeInvalid;
            }
            if (!TryDecimal(r["Kinh độ"], out var lng) || lng is < -180 or > 180)
            {
                return ConstantHelper.Message.LongitudeInvalid;
            }
            string code = await CodeOrNewAsync(r["Mã điểm giao"], DocumentKind.DeliveryLocation);
            var e = await FindAsync<CustomerDeliveryLocation>(x => x.CustomerId == customer.Id && x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.CustomerDeliveryLocationRepository, new CustomerDeliveryLocation { Id = Guid.NewGuid(), CustomerId = customer.Id, Code = code, Status = Active });
            bool changed = Set(e, x => x.Name, r["Tên điểm giao"]) | Set(e, x => x.Address, r["Địa chỉ"]) | Set(e, x => x.ContactName, r["Người nhận"])
                | Set(e, x => x.ContactPhone, r["SĐT người nhận"]) | Set(e, x => x.DeliveryWindowFrom, from) | Set(e, x => x.DeliveryWindowTo, to)
                | Set(e, x => x.Latitude, lat) | Set(e, x => x.Longitude, lng) | Set(e, x => x.DeliveryInstructions, r["Hướng dẫn giao"]);
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> SupplierAsync(TemplateRow r, Ctx ctx)
        {
            string supplierCode = await CodeOrNewAsync(r["Mã NCC"], DocumentKind.Supplier);
            var (supplierType, supplierTypeError) = await RefAsync<Supplier>("Supplier.SupplierType", r["Loại NCC"], x => x.Code == supplierCode && !x.IsDeleted, x => x.SupplierType);
            if (supplierTypeError != null)
            {
                return supplierTypeError;
            }
            var (e, isNew) = await SupplierByCodeAsync(supplierCode, r["Tên NCC"]!, ctx);
            bool changed = Set(e, x => x.Name, r["Tên NCC"]) | Set(e, x => x.SupplierType, supplierType) | Set(e, x => x.TaxCode, r["Mã số thuế"])
                | Set(e, x => x.Phone, r["Điện thoại"]) | Set(e, x => x.Email, r["Email"]) | Set(e, x => x.Address, r["Địa chỉ"])
                | Set(e, x => x.ContactName, r["Người liên hệ"]) | Set(e, x => x.ContactPhone, r["SĐT liên hệ"]);
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> UnitMeasureAsync(TemplateRow r, Ctx ctx)
        {
            string code = r["Mã ĐVT"]!;
            if (!TryInt(r["Số chữ số thập phân"], out var precision))
            {
                return "Số chữ số thập phân phải là số nguyên.";
            }
            var e = await FindAsync<UnitMeasure>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.UnitMeasureRepository, new UnitMeasure { Id = Guid.NewGuid(), Code = code, Status = Active });
            bool changed = Set(e, x => x.Name, r["Tên ĐVT"]) | Set(e, x => x.Symbol, r["Ký hiệu"]) | Set(e, x => x.DecimalPrecision, precision);
            ctx.Units[code] = e;
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> IngredientAsync(TemplateRow r, Ctx ctx)
        {
            if (!TryPercent(r["Tỷ lệ thu hồi (%)"], out var yield) || yield is <= 0 or > 100)
            {
                return ConstantHelper.Message.YieldPercentRange;
            }
            if (!TryPercent(r["Hao hụt chuẩn (%)"], out var waste) || waste is < 0 or >= 100)
            {
                return ConstantHelper.Message.StandardWasteRange;
            }
            bool expiry = ParseBool(r["Quản lý hạn dùng"]);
            if (!TryInt(r["Số ngày hạn dùng"], out var shelf) || (expiry && !(shelf > 0)))
            {
                return ConstantHelper.Message.ShelfLifeRequired;
            }
            if (!TryDecimal(r["Tồn tối thiểu"], out var min))
            {
                return "Tồn tối thiểu phải là số.";
            }
            var unit = r["ĐVT"] == null ? null : await UnitAsync(r["ĐVT"]!, ctx);
            var category = r["Nhóm nguyên liệu"] == null ? null : await IngredientCategoryAsync(r["Nhóm nguyên liệu"]!, ctx);
            string ingredientCode = await CodeOrNewAsync(r["Mã nguyên liệu"], DocumentKind.Ingredient);
            var (storageType, storageTypeError) = await RefAsync<Ingredient>("Ingredient.StorageType", r["Kiểu bảo quản"], x => x.Code == ingredientCode && !x.IsDeleted, x => x.StorageType);
            if (storageTypeError != null)
            {
                return storageTypeError;
            }
            var (e, isNew) = await IngredientByCodeAsync(ingredientCode, r["Tên nguyên liệu"]!, unit, ctx);
            bool changed = Set(e, x => x.Name, r["Tên nguyên liệu"]) | Set(e, x => x.StorageType, storageType) | Set(e, x => x.YieldPercent, yield)
                | Set(e, x => x.StandardWastePercent, waste) | Set(e, x => x.IsExpiryManaged, expiry) | Set(e, x => x.ShelfLifeDays, shelf) | Set(e, x => x.MinimumStock, min);
            if (unit != null)
            {
                changed |= Set(e, x => x.BaseUnitId, unit.Id) | Set(e, x => x.BaseUomId, unit.Id); // ghi song song cột cũ (DEPRECATED_COLUMNS)
            }
            if (category != null)
            {
                changed |= Set(e, x => x.IngredientCategoryId, category.Id) | Set(e, x => x.CategoryId, category.Id);
            }
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> WarehouseAsync(TemplateRow r, Ctx ctx)
        {
            Guid? unitId = null;
            if (r["Mã đơn vị"] is { } orgCode)
            {
                unitId = await _unitOfWork.OrganizationUnitRepository.GetAll().Where(x => x.Code == orgCode && !x.IsDeleted).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
                if (unitId == null)
                {
                    return string.Format(ConstantHelper.Message.NotExist, "Đơn vị " + orgCode);
                }
            }
            string code = await CodeOrNewAsync(r["Mã kho"], DocumentKind.Warehouse);
            var (warehouseType, warehouseTypeError) = await RefAsync<Warehouse>("Warehouse.WarehouseType", r["Loại kho"], x => x.Code == code && !x.IsDeleted, x => x.WarehouseType);
            if (warehouseTypeError != null)
            {
                return warehouseTypeError;
            }
            var e = await FindAsync<Warehouse>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.WarehouseRepository, new Warehouse { Id = Guid.NewGuid(), Code = code, Status = Active });
            bool changed = Set(e, x => x.Name, r["Tên kho"]) | Set(e, x => x.WarehouseType, warehouseType) | Set(e, x => x.Address, r["Địa chỉ"])
                | Set(e, x => x.OrganizationUnitId, unitId ?? e.OrganizationUnitId);
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> EmployeeAsync(TemplateRow r, Ctx ctx)
        {
            Guid? unitId = null, positionId = null;
            if (r["Mã đơn vị"] is { } orgCode && (unitId = await _unitOfWork.OrganizationUnitRepository.GetAll().Where(x => x.Code == orgCode && !x.IsDeleted).Select(x => (Guid?)x.Id).FirstOrDefaultAsync()) == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Đơn vị " + orgCode);
            }
            if (r["Mã chức vụ"] is { } posCode && (positionId = await _unitOfWork.PositionRepository.GetAll().Where(x => x.Code == posCode && !x.IsDeleted).Select(x => (Guid?)x.Id).FirstOrDefaultAsync()) == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Chức vụ " + posCode);
            }
            if (!TryDate(r["Ngày vào làm"], out var hire))
            {
                return "Ngày vào làm phải dạng yyyy-MM-dd.";
            }
            string code = await CodeOrNewAsync(r["Mã nhân viên"], DocumentKind.Employee);
            var e = await FindAsync<Employee>(x => (x.Code == code || x.EmployeeCode == code) && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.EmployeeRepository, new Employee { Id = Guid.NewGuid(), Code = code, Status = Active });
            bool changed = Set(e, x => x.Code, code) | Set(e, x => x.EmployeeCode, code) | Set(e, x => x.FullName, r["Họ tên"])
                | Set(e, x => x.OrganizationUnitId, unitId ?? e.OrganizationUnitId) | Set(e, x => x.PositionId, positionId ?? e.PositionId)
                | Set(e, x => x.Phone, r["Điện thoại"]) | Set(e, x => x.Email, r["Email"]) | Set(e, x => x.HireDate, hire ?? e.HireDate);
            return Finish(e, isNew, changed, ctx);
        }

        /// <summary>
        /// BOT-3.3 (D-72): 1 dòng máy chấm công → AttendanceEvent (SourceChannel MACHINE, hợp lệ – máy đặt tại bếp). Khớp ca được phân của nhân viên trong ngày
        /// (hoặc hôm trước – ca đêm) có khung [giờ bắt đầu − 3 giờ, giờ kết thúc + 3 giờ] chứa thời điểm. Chống trùng theo ExternalEventId MACHINE:mã:yyyyMMddHHmm.
        /// Vào ca → ca Đã vào ca; Ra ca → ca Hoàn thành (giống chấm GPS / QR).
        /// </summary>
        private async Task<string?> AttendanceMachineAsync(TemplateRow r, Ctx ctx)
        {
            string code = r["Mã nhân viên"]!.Trim();
            string raw = r["Thời điểm"]!.Trim();
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                && !DateTime.TryParseExact(raw, ["dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out at))
            {
                return "Thời điểm phải dạng yyyy-MM-dd HH:mm hoặc dd/MM/yyyy HH:mm.";
            }
            string? type = (r["Loại"] ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "" => null,
                "vào" or "vao" or "in" or "check_in" => AttendanceEventType.CheckIn,
                "ra" or "out" or "check_out" => AttendanceEventType.CheckOut,
                _ => "?",
            };
            if (type == "?")
            {
                return "Loại phải là Vào hoặc Ra (hoặc để trống).";
            }
            var db = _unitOfWork.DbContext;
            var employee = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => (e.Code == code || e.EmployeeCode == code) && !e.IsDeleted);
            if (employee == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Nhân viên " + code);
            }
            string key = $"{AttendanceSource.Machine}:{code}:{at:yyyyMMddHHmm}";
            if (ctx.AttendanceKeys.Contains(key) || await db.AttendanceEvent.AsNoTracking().AnyAsync(e => !e.IsDeleted && e.ExternalEventId == key))
            {
                ctx.Result.Skipped++;
                return null;
            }
            DateTime day = at.Date;
            var assignment = (await db.ShiftAssignment
                    .Where(a => !a.IsDeleted && a.EmployeeId == employee.Id && a.WorkDate >= day.AddDays(-1) && a.WorkDate <= day && a.AssignmentStatus != ShiftAssignmentStatus.Absent)
                    .ToListAsync())
                .Where(a => a.PlannedStart is { } s && a.PlannedEnd is { } e2 && at >= s.AddHours(-3) && at <= e2.AddHours(3))
                .OrderBy(a => Math.Abs((at - a.PlannedStart!.Value).TotalMinutes))
                .FirstOrDefault();
            if (assignment == null)
            {
                return $"Nhân viên {code} không có ca được phân quanh {at:dd/MM HH:mm}.";
            }
            bool hasIn = ctx.CheckedInAssignments.Contains(assignment.Id)
                || await db.AttendanceEvent.AsNoTracking().AnyAsync(e => !e.IsDeleted && e.ShiftAssignmentId == assignment.Id && e.EventType == AttendanceEventType.CheckIn);
            type ??= hasIn ? AttendanceEventType.CheckOut : AttendanceEventType.CheckIn;
            db.AttendanceEvent.Add(new AttendanceEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                ShiftAssignmentId = assignment.Id,
                EventType = type,
                EventAt = at,
                IsValid = true,
                SourceChannel = AttendanceSource.Machine,
                ExternalEventId = key,
                Note = "Máy chấm công" + (r["Mã máy"] is { } m && m.Length > 0 ? " " + m.Trim() : string.Empty),
            });
            if (type == AttendanceEventType.CheckIn)
            {
                ctx.CheckedInAssignments.Add(assignment.Id);
                if (ShiftAssignmentStatus.Open.Contains(assignment.AssignmentStatus))
                {
                    assignment.AssignmentStatus = ShiftAssignmentStatus.CheckedIn;
                }
            }
            else if (assignment.AssignmentStatus == ShiftAssignmentStatus.CheckedIn)
            {
                assignment.AssignmentStatus = ShiftAssignmentStatus.Completed;
            }
            ctx.AttendanceKeys.Add(key);
            ctx.Result.Inserted++;
            return null;
        }

        private async Task<string?> VehicleAsync(TemplateRow r, Ctx ctx)
        {
            if (!TryDecimal(r["Tải trọng (kg)"], out var capacity))
            {
                return "Tải trọng phải là số.";
            }
            string code = await CodeOrNewAsync(r["Mã xe"], DocumentKind.Vehicle);
            var e = await FindAsync<Vehicle>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.VehicleRepository, new Vehicle { Id = Guid.NewGuid(), Code = code, Status = Active });
            bool changed = Set(e, x => x.LicensePlate, r["Biển số"]) | Set(e, x => x.VehicleType, r["Loại xe"]) | Set(e, x => x.Capacity, capacity);
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<string?> DishAsync(TemplateRow r, Ctx ctx)
        {
            if (!TryDecimal(r["Định lượng chuẩn (g/suất)"], out var portion))
            {
                return "Định lượng chuẩn phải là số.";
            }
            var category = r["Nhóm món"] == null ? null : await DishCategoryAsync(r["Nhóm món"]!, ctx);
            string code = await CodeOrNewAsync(r["Mã món"], DocumentKind.Dish);
            var e = await FindAsync<Dish>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.DishRepository, new Dish { Id = Guid.NewGuid(), Code = code, Status = Active });
            bool changed = Set(e, x => x.Name, r["Tên món"]) | Set(e, x => x.StandardPortion, portion) | Set(e, x => x.Description, r["Mô tả"]);
            if (category != null)
            {
                changed |= Set(e, x => x.DishCategoryId, category.Id) | Set(e, x => x.CategoryId, category.Id);
            }
            ctx.Dishes[code] = e;
            return Finish(e, isNew, changed, ctx);
        }

        /// <summary>
        /// Định mức: dòng của công thức Draft của món (chưa có công thức → tạo bản Draft V1). Món đang có bản khóa / chờ duyệt mà không có Draft
        /// → báo lỗi (tạo phiên bản mới trên màn hình trước). Idempotent theo (công thức, nguyên liệu).
        /// </summary>
        private async Task<string?> RecipeLineAsync(TemplateRow r, Ctx ctx)
        {
            var dish = await FindAsync<Dish>(x => x.Code == r["Mã món"] && !x.IsDeleted);
            if (dish == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Món " + r["Mã món"]);
            }
            var ingredient = await FindAsync<Ingredient>(x => x.Code == r["Mã nguyên liệu"] && !x.IsDeleted);
            if (ingredient == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Nguyên liệu " + r["Mã nguyên liệu"]);
            }
            if (!TryDecimal(r["Định lượng"], out var qty) || !(qty > 0))
            {
                return ConstantHelper.Message.QuantityMustBePositive;
            }
            if (!TryPercent(r["% hao hụt sơ chế"], out var waste) || waste is < 0 or >= 100 || !TryPercent(r["% hao hụt chế biến"], out var cookWaste) || cookWaste is < 0 or >= 100)
            {
                return ConstantHelper.Message.WastePercentRange;
            }
            var unit = await UnitAsync(r["ĐVT"]!, ctx);
            var recipes = await _unitOfWork.RecipeRepository.GetAll().Where(x => x.DishId == dish.Id && !x.IsDeleted).ToListAsync();
            var draft = recipes.FirstOrDefault(x => x.Status == RecipeStatus.Draft)
                ?? _unitOfWork.DbContext.ChangeTracker.Entries<Recipe>().Select(x => x.Entity).FirstOrDefault(x => x.DishId == dish.Id && x.Status == RecipeStatus.Draft && !x.IsDeleted);
            if (draft == null)
            {
                if (recipes.Count != 0)
                {
                    return "Món đã có công thức đã duyệt / khóa – tạo phiên bản mới (Recipe/CreateVersion) rồi nhập định mức.";
                }
                draft = Add(_unitOfWork.RecipeRepository, new Recipe
                {
                    Id = Guid.NewGuid(), DishId = dish.Id, VersionNo = 1, VersionCode = $"{dish.Code}-V1", Status = RecipeStatus.Draft, IsActive = false,
                    StandardOutputQty = _rules.RecipeStandardServings, YieldQuantity = _rules.RecipeStandardServings,
                });
                ctx.Result.ListNote.Add($"Tạo công thức Draft {draft.VersionCode}.");
            }
            var line = await FindAsync<RecipeLine>(x => x.RecipeId == draft.Id && x.IngredientId == ingredient.Id && !x.IsDeleted);
            bool isNew = line == null;
            if (line == null)
            {
                int sort = await _unitOfWork.RecipeLineRepository.GetAll().Where(x => x.RecipeId == draft.Id && !x.IsDeleted).MaxAsync(x => (int?)x.SortOrder) ?? 0;
                sort = Math.Max(sort, _unitOfWork.DbContext.ChangeTracker.Entries<RecipeLine>().Where(x => x.Entity.RecipeId == draft.Id).Select(x => x.Entity.SortOrder).DefaultIfEmpty(0).Max());
                line = Add(_unitOfWork.RecipeLineRepository, new RecipeLine { Id = Guid.NewGuid(), RecipeId = draft.Id, IngredientId = ingredient.Id, SortOrder = sort + 1, SequenceNo = sort + 1 });
            }
            bool changed = Set(line, x => x.Quantity, qty!.Value) | Set(line, x => x.UnitId, unit.Id) | Set(line, x => x.UomId, unit.Id)
                | Set(line, x => x.WastePercent, waste) | Set(line, x => x.ExpectedWasteRate, cookWaste) | Set(line, x => x.PreparationNote, r["Ghi chú sơ chế"]);
            return Finish(line, isNew, changed, ctx);
        }

        private async Task<string?> SupplierMaterialAsync(TemplateRow r, Ctx ctx)
        {
            var supplier = await FindAsync<Supplier>(x => x.Code == r["Mã NCC"] && !x.IsDeleted);
            if (supplier == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "NCC " + r["Mã NCC"]);
            }
            var ingredient = await FindAsync<Ingredient>(x => x.Code == r["Mã nguyên liệu"] && !x.IsDeleted);
            if (ingredient == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Nguyên liệu " + r["Mã nguyên liệu"]);
            }
            if (!TryDecimal(r["Đơn giá (chưa VAT)"], out var price) || !(price > 0))
            {
                return ConstantHelper.Message.PriceMustBePositive;
            }
            if (!TryDate(r["Hiệu lực từ"], out var from) || !TryDate(r["Hiệu lực đến"], out var to) || (from != null && to != null && from > to))
            {
                return "Ngày hiệu lực phải dạng yyyy-MM-dd và từ ≤ đến.";
            }
            if (!TryDecimal(r["Số lượng đặt tối thiểu"], out var moq))
            {
                return "Số lượng đặt tối thiểu phải là số.";
            }
            var unit = r["ĐVT mua"] == null ? null : await UnitAsync(r["ĐVT mua"]!, ctx);
            return await UpsertSupplierMaterialAsync(supplier, ingredient, price!.Value, unit, from, to, ParseBool(r["Ưu tiên"]), moq, ctx);
        }

        // ============================================================ File vận hành thật
        private async Task<string?> ImportRealAsync(Kind kind, XLWorkbook workbook, Ctx ctx)
        {
            var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals(kind.RealSheet, StringComparison.OrdinalIgnoreCase));
            if (ws == null)
            {
                return $"File không có sheet \"{kind.RealSheet}\".";
            }
            return kind.TableName switch
            {
                "RuleMenu_DishBank" => await DishBankAsync(ws, ctx),
                "RuleMenu_DishDetail" => await DishDetailAsync(ws, ctx),
                "BaoGia_Summary" => await PriceSummaryAsync(ws, ctx),
                _ => "Loại file chưa hỗ trợ.",
            };
        }

        /// <summary>Ngân hàng món: tiêu đề dòng 1 = nhóm món, mỗi ô dưới = 1 món. Món nhận diện theo tên (mã sinh từ tên, không dấu).</summary>
        private async Task<string?> DishBankAsync(IXLWorksheet ws, Ctx ctx)
        {
            var header = ws.Row(1).CellsUsed().Where(c => CellText(c).Trim().Length > 0).ToList();
            if (header.Count == 0)
            {
                return "Dòng 1 phải là tên các nhóm món.";
            }
            foreach (var h in header)
            {
                var category = await DishCategoryAsync(CellText(h).Trim(), ctx);
                foreach (var cell in ws.Column(h.Address.ColumnNumber).CellsUsed().Where(c => c.Address.RowNumber > 1))
                {
                    string name = CellText(cell).Trim();
                    if (name.Length == 0)
                    {
                        continue;
                    }
                    ctx.Result.TotalRows++;
                    string? err = await DishByNameAsync(name, category, ctx);
                    if (err != null)
                    {
                        ctx.Error(cell.Address.RowNumber, $"{cell.Address}: {err}");
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Chi tiết món: khối nguyên liệu L–Q (L Mã, M Tên, N ĐVT, P % hao hụt sơ chế) → Ingredient theo mã;
        /// khối món A–H (B Nhóm, D Tên món) → DishCategory / Dish theo tên. File KHÔNG có định lượng → không tạo RecipeLine (GAP-IMP-01).
        /// </summary>
        private async Task<string?> DishDetailAsync(IXLWorksheet ws, Ctx ctx)
        {
            if (Norm(CellText(ws.Cell("L1"))) != Norm("Mã") || Norm(CellText(ws.Cell("D1"))) != Norm("Tên món"))
            {
                return "Sheet không đúng bố cục: cần D1 = \"Tên món\", L1 = \"Mã\".";
            }
            int components = 0;
            var dishSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > 1))
            {
                int n = row.RowNumber();
                string code = CellText(row.Cell("L")).Trim();
                if (code.Length > 0)
                {
                    ctx.Result.TotalRows++;
                    string name = CellText(row.Cell("M")).Trim();
                    if (name.Length == 0)
                    {
                        ctx.Error(n, $"Nguyên liệu {code}: thiếu tên (cột M).");
                    }
                    else if (!TryPercent(CellText(row.Cell("P")), out var waste) || waste is < 0 or >= 100)
                    {
                        ctx.Error(n, $"Nguyên liệu {code}: % hao hụt sơ chế (cột P) không hợp lệ.");
                    }
                    else
                    {
                        string unitName = CellText(row.Cell("N")).Trim();
                        var unit = unitName.Length == 0 ? null : await UnitAsync(unitName, ctx);
                        var (e, isNew) = await IngredientByCodeAsync(code, name, unit, ctx);
                        bool changed = Set(e, x => x.Name, name) | Set(e, x => x.StandardWastePercent, waste);
                        if (unit != null)
                        {
                            changed |= Set(e, x => x.BaseUnitId, unit.Id) | Set(e, x => x.BaseUomId, unit.Id);
                        }
                        string? err = Finish(e, isNew, changed, ctx);
                        if (err != null)
                        {
                            ctx.Error(n, err);
                        }
                    }
                }
                string dishName = CellText(row.Cell("D")).Trim();
                if (dishName.Length > 0)
                {
                    components++;
                    if (dishSeen.Add(dishName))
                    {
                        ctx.Result.TotalRows++;
                        string groupName = CellText(row.Cell("B")).Trim();
                        var category = groupName.Length == 0 ? null : await DishCategoryAsync(groupName, ctx);
                        string? err = await DishByNameAsync(dishName, category, ctx);
                        if (err != null)
                        {
                            ctx.Error(n, err);
                        }
                    }
                }
            }
            ctx.Result.ListNote.Add($"GAP-IMP-01: {components} dòng thành phần món chưa nạp vào định mức vì file không có cột định lượng. "
                + "Nhập định mức bằng mẫu RecipeLine (Mã món, Mã NL, Định lượng, ĐVT, % hao hụt). Cột Giá (O) không nạp vì không gắn NCC – dùng bảng giá NCC.");
            return null;
        }

        /// <summary>Tổng hợp báo giá – sheet Summary: B Mã NL, C Tên, D ĐVT chính, E Đơn giá chưa VAT, I NCC, J Ma_NCC → Supplier, Ingredient, SupplierMaterial.</summary>
        private async Task<string?> PriceSummaryAsync(IXLWorksheet ws, Ctx ctx)
        {
            if (Norm(CellText(ws.Cell("B1"))) != Norm("Mã") || Norm(CellText(ws.Cell("J1"))) != Norm("Ma_NCC"))
            {
                return "Sheet Summary không đúng bố cục: cần B1 = \"Mã\", J1 = \"Ma_NCC\".";
            }
            foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > 1))
            {
                int n = row.RowNumber();
                string code = CellText(row.Cell("B")).Trim(), supplierCode = CellText(row.Cell("J")).Trim();
                if (code.Length == 0 && supplierCode.Length == 0)
                {
                    continue;
                }
                ctx.Result.TotalRows++;
                if (code.Length == 0 || supplierCode.Length == 0)
                {
                    ctx.Error(n, "Thiếu mã nguyên liệu (B) hoặc mã NCC (J).");
                    continue;
                }
                if (!TryDecimal(CellText(row.Cell("E")), out var price) || !(price > 0))
                {
                    ctx.Error(n, ConstantHelper.Message.PriceMustBePositive + " (cột E).");
                    continue;
                }
                string unitName = CellText(row.Cell("D")).Trim();
                var unit = unitName.Length == 0 ? null : await UnitAsync(unitName, ctx);
                string supplierName = CellText(row.Cell("I")).Trim();
                var (supplier, supplierNew) = await SupplierByCodeAsync(supplierCode, supplierName.Length == 0 ? supplierCode : supplierName, ctx);
                if (supplierNew)
                {
                    ctx.Result.ListNote.Add($"Tạo NCC {supplierCode}.");
                }
                var (ingredient, ingredientNew) = await IngredientByCodeAsync(code, CellText(row.Cell("C")).Trim() is { Length: > 0 } nm ? nm : code, unit, ctx);
                if (ingredientNew)
                {
                    ctx.Result.ListNote.Add($"Tạo nguyên liệu {code} (chưa có trong danh mục).");
                }
                string? err = await UpsertSupplierMaterialAsync(supplier, ingredient, price!.Value, unit, null, null, preferred: false, moq: null, ctx);
                if (err != null)
                {
                    ctx.Error(n, err);
                }
            }
            ctx.Result.ListNote.Add("% VAT (cột F) chưa nạp: bảng SupplierMaterial không có cột VAT (GAP-IMP-02). NCC ưu tiên chọn trên màn hình bảng giá.");
            return null;
        }

        // ============================================================ tra cứu / tạo dùng chung
        private async Task<UnitMeasure> UnitAsync(string codeOrName, Ctx ctx)
        {
            string key = codeOrName.Trim();
            if (ctx.Units.TryGetValue(key, out var cached))
            {
                return cached;
            }
            var unit = await FindAsync<UnitMeasure>(x => !x.IsDeleted && (x.Code == key || x.Name == key || x.Symbol == key));
            if (unit == null)
            {
                unit = Add(_unitOfWork.UnitMeasureRepository, new UnitMeasure { Id = Guid.NewGuid(), Code = Slug(key, 50), Name = key, Symbol = key, Status = Active });
                ctx.Result.ListNote.Add($"Tạo ĐVT \"{key}\".");
            }
            return ctx.Units[key] = unit;
        }

        private async Task<IngredientCategory> IngredientCategoryAsync(string name, Ctx ctx)
        {
            if (ctx.IngredientCategories.TryGetValue(name, out var cached))
            {
                return cached;
            }
            var e = await FindAsync<IngredientCategory>(x => !x.IsDeleted && (x.Name == name || x.Code == name))
                ?? Add(_unitOfWork.IngredientCategoryRepository, new IngredientCategory { Id = Guid.NewGuid(), Code = await _documentNumberService.NextCodeAsync(DocumentKind.IngredientCategory), Name = name, Status = Active });
            return ctx.IngredientCategories[name] = e;
        }

        private async Task<DishCategory> DishCategoryAsync(string name, Ctx ctx)
        {
            if (ctx.DishCategories.TryGetValue(name, out var cached))
            {
                return cached;
            }
            var e = await FindAsync<DishCategory>(x => !x.IsDeleted && (x.Name == name || x.Code == name));
            if (e == null)
            {
                e = Add(_unitOfWork.DishCategoryRepository, new DishCategory { Id = Guid.NewGuid(), Code = await _documentNumberService.NextCodeAsync(DocumentKind.DishCategory), Name = name, Status = Active });
                ctx.Result.Inserted++;
            }
            return ctx.DishCategories[name] = e;
        }

        /// <summary>Món theo tên (file thật không có mã món): chưa có → tạo, mã = tên không dấu (duy nhất).</summary>
        private async Task<string?> DishByNameAsync(string name, DishCategory? category, Ctx ctx)
        {
            if (ctx.Dishes.TryGetValue("name:" + name, out var known))
            {
                ctx.Result.Skipped++;
                return null;
            }
            var e = await FindAsync<Dish>(x => x.Name == name && !x.IsDeleted);
            bool isNew = e == null;
            if (e == null)
            {
                string code = Slug(name, 60);
                for (int i = 2; await AnyAsync<Dish>(x => x.Code == code) || ctx.Dishes.ContainsKey(code); i++)
                {
                    code = Slug(name, 55) + "-" + i;
                }
                e = Add(_unitOfWork.DishRepository, new Dish { Id = Guid.NewGuid(), Code = code, Name = name, Status = Active });
                ctx.Dishes[code] = e;
            }
            bool changed = false;
            if (category != null && e.DishCategoryId == null)
            {
                changed = Set(e, x => x.DishCategoryId, category.Id) | Set(e, x => x.CategoryId, category.Id);
            }
            ctx.Dishes["name:" + name] = e;
            return Finish(e, isNew, changed, ctx);
        }

        private async Task<(Supplier Entity, bool IsNew)> SupplierByCodeAsync(string code, string name, Ctx ctx)
        {
            if (ctx.Suppliers.TryGetValue(code, out var cached))
            {
                return (cached, false);
            }
            var e = await FindAsync<Supplier>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.SupplierRepository, new Supplier { Id = Guid.NewGuid(), Code = code, Name = name, Status = Active });
            return (ctx.Suppliers[code] = e, isNew);
        }

        private async Task<(Ingredient Entity, bool IsNew)> IngredientByCodeAsync(string code, string name, UnitMeasure? unit, Ctx ctx)
        {
            if (ctx.Ingredients.TryGetValue(code, out var cached))
            {
                return (cached, false);
            }
            var e = await FindAsync<Ingredient>(x => x.Code == code && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.IngredientRepository, new Ingredient { Id = Guid.NewGuid(), Code = code, Name = name, BaseUnitId = unit?.Id, BaseUomId = unit?.Id, Status = Active });
            return (ctx.Ingredients[code] = e, isNew);
        }

        private async Task<string?> UpsertSupplierMaterialAsync(Supplier supplier, Ingredient ingredient, decimal price, UnitMeasure? unit,
            DateTime? from, DateTime? to, bool preferred, decimal? moq, Ctx ctx)
        {
            var e = await FindAsync<SupplierMaterial>(x => x.SupplierId == supplier.Id && x.IngredientId == ingredient.Id && !x.IsDeleted);
            bool isNew = e == null;
            e ??= Add(_unitOfWork.SupplierMaterialRepository, new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier.Id, IngredientId = ingredient.Id, Status = Active });
            bool changed = Set(e, x => x.UnitPrice, price) | Set(e, x => x.PurchaseUomId, unit?.Id ?? e.PurchaseUomId)
                | Set(e, x => x.EffectiveFrom, from ?? e.EffectiveFrom) | Set(e, x => x.EffectiveTo, to ?? e.EffectiveTo) | Set(e, x => x.MinimumOrderQuantity, moq ?? e.MinimumOrderQuantity);
            if (preferred && !e.IsPreferred)
            {
                // Mỗi nguyên liệu 1 NCC ưu tiên (như SupplierMaterialService).
                var dbOthers = await _unitOfWork.SupplierMaterialRepository.GetAll().Where(x => x.IngredientId == ingredient.Id && x.IsPreferred && x.Id != e.Id && !x.IsDeleted).ToListAsync();
                var localOthers = _unitOfWork.DbContext.Set<SupplierMaterial>().Local.Where(x => x.IngredientId == ingredient.Id && x.IsPreferred && x.Id != e.Id);
                foreach (var other in dbOthers.Concat(localOthers).Distinct())
                {
                    other.IsPreferred = false;
                }
                e.IsPreferred = true;
                changed = true;
            }
            return Finish(e, isNew, changed, ctx);
        }

        // ============================================================ tiện ích
        /// <summary>Tìm trong bộ nhớ EF trước (bản ghi vừa thêm / sửa ở dòng trước của cùng file, chưa lưu) rồi mới tới DB.</summary>
        private async Task<T?> FindAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class
            => _unitOfWork.DbContext.Set<T>().Local.FirstOrDefault(predicate.Compile()) ?? await _unitOfWork.DbContext.Set<T>().FirstOrDefaultAsync(predicate);

        private async Task<bool> AnyAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class
            => _unitOfWork.DbContext.Set<T>().Local.Any(predicate.Compile()) || await _unitOfWork.DbContext.Set<T>().AnyAsync(predicate);

        private static T Add<T>(IRepository<T> repository, T entity) where T : class
        {
            repository.Add(entity);
            return entity;
        }

        /// <summary>Gán khi khác giá trị hiện tại (null = giữ nguyên với chuỗi). Trả true nếu đổi.</summary>
        private static bool Set<TEntity, TValue>(TEntity entity, System.Linq.Expressions.Expression<Func<TEntity, TValue>> property, TValue value)
        {
            if (value is null || (value is string s && s.Length == 0))
            {
                return false;
            }
            var member = (PropertyInfo)((System.Linq.Expressions.MemberExpression)property.Body).Member;
            var current = (TValue?)member.GetValue(entity);
            if (EqualityComparer<TValue>.Default.Equals(current, value))
            {
                return false;
            }
            member.SetValue(entity, value);
            return true;
        }

        /// <summary>Kiểm độ dài chuỗi theo [MaxLength] của entity trước khi lưu (lỗi ở 1 dòng không làm hỏng cả lần lưu); đếm kết quả.</summary>
        private string? Finish(object entity, bool isNew, bool changed, Ctx ctx)
        {
            foreach (var p in entity.GetType().GetProperties())
            {
                int? max = p.GetCustomAttribute<MaxLengthAttribute>()?.Length ?? p.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
                if (max is > 0 && p.GetValue(entity) is string s && s.Length > max)
                {
                    if (isNew)
                    {
                        _unitOfWork.DbContext.Entry(entity).State = EntityState.Detached;
                    }
                    else
                    {
                        _unitOfWork.DbContext.Entry(entity).Reload();
                    }
                    return $"{p.Name} tối đa {max} ký tự.";
                }
            }
            ctx.Count(isNew, changed);
            return null;
        }

        private static string CellText(IXLCell cell)
        {
            var v = cell.Value;
            if (v.IsBlank) return string.Empty;
            if (v.IsNumber) return v.GetNumber().ToString(CultureInfo.InvariantCulture);
            if (v.IsDateTime) return v.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (v.IsTimeSpan) return v.GetTimeSpan().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            if (v.IsBoolean) return v.GetBoolean() ? "true" : "false";
            if (v.IsError) return string.Empty;
            return v.GetText();
        }

        /// <summary>Chuẩn hóa tiêu đề cột: bỏ "*", khoảng trắng thừa, xuống dòng; không phân biệt hoa thường.</summary>
        private static string Norm(string header) => Spaces().Replace(header.Replace("*", string.Empty), " ").Trim().ToLowerInvariant();

        private static string Slug(string text, int max)
        {
            string s = CommonHelper.RemoveSign4VietnameseString(text).ToUpperInvariant();
            s = NonAlnum().Replace(s, "-").Trim('-');
            return s.Length <= max ? s : s[..max].Trim('-');
        }

        private static bool ParseBool(string? v) => v != null && new[] { "có", "co", "x", "1", "true", "yes", "y" }.Contains(v.Trim().ToLowerInvariant());

        private static bool TryDecimal(string? raw, out decimal? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;
            string s = raw.Trim().Replace(" ", string.Empty);
            if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
            else s = s.Replace(",", string.Empty);
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
            return false;
        }

        /// <summary>% hoặc tỷ lệ: 0 &lt; x ≤ 1 hiểu là tỷ lệ (0,05 = 5%), &gt; 1 là %. Trống → null.</summary>
        private static bool TryPercent(string? raw, out decimal? value)
        {
            if (!TryDecimal(raw?.Replace("%", string.Empty), out value)) return false;
            if (value is > 0 and <= 1 && raw != null && !raw.Contains('%')) value *= 100;
            return true;
        }

        private static bool TryInt(string? raw, out int? value)
        {
            value = null;
            if (!TryDecimal(raw, out var d)) return false;
            if (d == null) return true;
            if (d != Math.Truncate(d.Value)) return false;
            value = (int)d.Value;
            return true;
        }

        private static bool TryDate(string? raw, out DateTime? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;
            if (DateTime.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                || DateTime.TryParseExact(raw.Trim(), ["dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                value = d.Date;
                return true;
            }
            return false;
        }

        private static bool TryTime(string? raw, out TimeSpan? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(raw)) return true;
            string s = raw.Trim();
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1)) { value = t; return true; }
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) { value = d.TimeOfDay; return true; }
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var frac) && frac is >= 0 and < 1) { value = TimeSpan.FromDays((double)frac); return true; }
            return false;
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Spaces();

        [GeneratedRegex(@"[^A-Z0-9]+")]
        private static partial Regex NonAlnum();
    }
}
