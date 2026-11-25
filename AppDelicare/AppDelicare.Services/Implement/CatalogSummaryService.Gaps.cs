using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.49 (9) (D-127 g): "Danh sách cần bổ sung" (Excel) từ trang Tổng quan danh mục – để NS / IT / Bếp trưởng tự bổ sung dữ liệu thật (hệ thống KHÔNG sửa hộ).
    /// Mỗi loại thiếu 1 sheet, chỉ gồm loại thuộc màn người dùng được Xem; sheet đầu "Tổng hợp" (loại · số dòng · ai bổ sung · màn).
    /// </summary>
    public partial class CatalogSummaryService
    {
        /// <summary>19 nhóm quyền chuẩn (ORG_STRUCTURE §2, D-79 / D-124).</summary>
        public static readonly string[] StandardGroupCodes =
        [
            "CEO", "CEO-PMO", "COO", "OPS-PLAN", "OPS-SCM", "OPS-WH", "OPS-SCM-HEAD", "OPS-KIT", "OPS-LOG", "OPS-POS", "OPS-MNT",
            "SAL-SALES", "SAL-CON", "SAL-HEAD", "SAL-CS", "FIN-ACC", "FIN-HR", "FIN-IT", "QA",
        ];

        private sealed record GapSheet(string Name, string Owner, string Screen, string[] Headers, List<object?[]> Rows);

        public async Task<IResult<FileContentModel>> ExportDataGaps()
        {
            var db = unitOfWork.DbContext;
            var perms = await permissions.GetAsync(currentActor.RequireActor().UserId);
            bool Can(string screen) => permissions.IsAllowed(perms, screen, PermissionAction.View);
            var sheets = new List<GapSheet>();
            var units = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).ToListAsync();
            var unitName = units.ToDictionary(u => u.Id, u => u.Name);
            if (Can("adm-orgunit"))
            {
                sheets.Add(new("Đơn vị chưa có trưởng", "Nhân sự", "Công ty & đơn vị", ["Mã đơn vị", "Tên đơn vị", "Loại", "Thuộc đơn vị"],
                    [.. units.Where(o => o.Status != "Inactive" && o.ManagerId == null).OrderBy(o => o.Code).Select(o => new object?[] { o.Code, o.Name, o.UnitType, o.ParentId is { } p ? unitName.GetValueOrDefault(p) : null })]));
            }
            var emps = Can("adm-employee") || Can("hr-shift-assign")
                ? await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.Status != EmployeeStatus.Resigned).ToListAsync() : [];
            if (Can("adm-employee"))
            {
                sheets.Add(new("Nhân viên chưa gắn đơn vị", "Nhân sự", "Nhân viên", ["Mã NV", "Họ tên", "Điện thoại", "Trạng thái"],
                    [.. emps.Where(e => e.OrganizationUnitId == null).OrderBy(e => e.Code ?? e.EmployeeCode).Select(e => new object?[] { e.Code ?? e.EmployeeCode, e.FullName, e.Phone, e.Status })]));
                var dup = emps.Select(e => (e, Phone: EmployeeRules.NormalizePhone(e.Phone))).Where(x => x.Phone != null).GroupBy(x => x.Phone!).Where(g => g.Count() > 1).ToList();
                sheets.Add(new("Số điện thoại trùng", "Nhân sự", "Nhân viên", ["Số điện thoại", "Mã NV", "Họ tên", "Đơn vị"],
                    [.. dup.OrderBy(g => g.Key).SelectMany(g => g.Select(x => new object?[] { g.Key, x.e.Code ?? x.e.EmployeeCode, x.e.FullName, x.e.OrganizationUnitId is { } u ? unitName.GetValueOrDefault(u) : null }))]));
            }
            if (Can("hr-shift-assign") || Can("hr-shift"))
            {
                var hr = rules.Value.Hr;
                var teams = emps.Where(e => e.OrganizationUnitId != null).GroupBy(e => e.OrganizationUnitId!.Value)
                    .Select(g => (Unit: units.FirstOrDefault(u => u.Id == g.Key), Count: g.Count()))
                    .Where(t => t.Unit != null && (t.Unit.Code == null || !hr.ShiftMinStaff.ContainsKey(t.Unit.Code))).OrderBy(t => t.Unit!.Code).ToList();
                sheets.Add(new("Tổ chưa có định biên ca", "Nhân sự (IT nhập cấu hình)", "Phân ca", ["Mã đơn vị", "Tên đơn vị", "Số nhân viên", "Ghi chú"],
                    [.. teams.Select(t => new object?[] { t.Unit!.Code, t.Unit.Name, t.Count, "Cần số người tối thiểu mỗi ca (BusinessRules:Hr:ShiftMinStaff)" })]));
            }
            if (Can("adm-usergroup"))
            {
                var groups = await db.UserGroup.AsNoTracking().Where(g => !g.IsDeleted).ToListAsync();
                var claims = await db.UserClaim.AsNoTracking().Where(c => !c.IsDeleted && c.UserGroupId != null).GroupBy(c => c.UserGroupId!.Value).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
                sheets.Add(new("Nhóm quyền ngoài chuẩn", "IT", "Nhóm quyền", ["Mã nhóm", "Tên nhóm", "Khối", "Số tài khoản", "Việc cần làm"],
                    [.. groups.Where(g => g.Code == null || !StandardGroupCodes.Contains(g.Code, StringComparer.OrdinalIgnoreCase) || g.GroupBlock == null).OrderBy(g => g.Code ?? g.Name)
                        .Select(g => new object?[] { g.Code, g.Name, g.GroupBlock, claims.FirstOrDefault(c => c.Key == g.Id)?.N ?? 0,
                            g.Code != null && StandardGroupCodes.Contains(g.Code, StringComparer.OrdinalIgnoreCase) ? "Chọn khối" : "Chuyển tài khoản sang nhóm chuẩn rồi ngừng dùng nhóm" })]));
            }
            if (Can("prod-line") || Can("menu-dish"))
            {
                sheets.Add(new("Nhóm món chưa gán dây chuyền", "Bếp trưởng", "Dây chuyền & bàn nấu", ["Mã nhóm món", "Tên nhóm món"],
                    [.. (await db.DishCategory.AsNoTracking().Where(d => !d.IsDeleted && d.ProductionLineId == null).OrderBy(d => d.Code).ToListAsync()).Select(d => new object?[] { d.Code, d.Name })]));
            }
            if (Can("menu-dish"))
            {
                sheets.Add(new("Món chưa phân nhóm", "Bếp trưởng", "Món ăn", ["Mã món", "Tên món"],
                    [.. (await db.Dish.AsNoTracking().Where(d => !d.IsDeleted && d.DishCategoryId == null && d.CategoryId == null).OrderBy(d => d.Code).ToListAsync()).Select(d => new object?[] { d.Code, d.Name })]));
            }
            if (Can("scm-ingredient"))
            {
                sheets.Add(new("Nguyên liệu chưa phân nhóm", "Bếp trưởng / Mua hàng", "Nguyên liệu", ["Mã nguyên liệu", "Tên nguyên liệu"],
                    [.. (await db.Ingredient.AsNoTracking().Where(i => !i.IsDeleted && i.IngredientCategoryId == null && i.CategoryId == null).OrderBy(i => i.Code).ToListAsync()).Select(i => new object?[] { i.Code, i.Name })]));
            }
            if (sheets.Count == 0)
            {
                return Result<FileContentModel>.Fail(string.Format(ConstantHelper.Message.CommonNoRight, "xem", "danh mục cần bổ sung"));
            }
            using var wb = new XLWorkbook();
            var sum = wb.Worksheets.Add("Tổng hợp");
            sum.Cell(1, 1).Value = "DANH SÁCH DỮ LIỆU CẦN BỔ SUNG TRƯỚC KHI CHẠY THẬT";
            sum.Cell(1, 1).Style.Font.Bold = true;
            sum.Cell(2, 1).Value = "Hệ thống không tự sửa dữ liệu thật – người phụ trách bổ sung trên màn tương ứng.";
            string[] head = ["Loại", "Số dòng", "Người bổ sung", "Màn hình"];
            for (int i = 0; i < head.Length; i++)
            {
                sum.Cell(4, i + 1).Value = head[i];
                sum.Cell(4, i + 1).Style.Font.Bold = true;
            }
            int r = 5;
            foreach (var s in sheets)
            {
                sum.Cell(r, 1).Value = s.Name;
                sum.Cell(r, 2).Value = s.Rows.Count;
                sum.Cell(r, 3).Value = s.Owner;
                sum.Cell(r, 4).Value = s.Screen;
                r++;
                var ws = wb.Worksheets.Add(s.Name.Length > 31 ? s.Name[..31] : s.Name);
                for (int i = 0; i < s.Headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = s.Headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                }
                for (int k = 0; k < s.Rows.Count; k++)
                {
                    for (int i = 0; i < s.Rows[k].Length; i++)
                    {
                        ws.Cell(k + 2, i + 1).Value = s.Rows[k][i] switch { null => Blank.Value, int n => n, decimal d => d, var o => o.ToString() };
                    }
                }
                ws.Columns().AdjustToContents(1, Math.Min(s.Rows.Count + 1, 200));
            }
            sum.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Result<FileContentModel>.Success(new FileContentModel
            {
                Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName = "DanhSachCanBoSung.xlsx",
            }, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
