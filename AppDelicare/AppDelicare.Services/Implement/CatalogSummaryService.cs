using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.40 (D-121): thẻ một danh mục trên trang tổng "Danh mục hệ thống".</summary>
    public class CatalogCardModel
    {
        public string Screen { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime? LastModified { get; set; }
        public string? LastModifiedBy { get; set; }
        public List<CatalogWarningModel> ListWarning { get; set; } = [];
    }

    public class CatalogWarningModel
    {
        public string Code { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Screen { get; set; } = string.Empty;
    }
}

namespace AppDelicare.Services.Interface
{
    public interface ICatalogSummaryService
    {
        Task<IResults<CatalogCardModel>> GetCatalogSummary();
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.40 (D-121): trang tổng "Danh mục hệ thống" – mỗi danh mục (theo quyền Xem của người gọi): số bản ghi đang dùng, sửa gần nhất,
    /// cảnh báo thiếu dữ liệu (chỉ khi tính được – không bịa số). Một lần gọi, truy vấn có tham số. Không thêm bảng / cột.
    /// </summary>
    public class CatalogSummaryService(IUnitOfWork unitOfWork, ICurrentActor currentActor, IPermissionService permissions, IOptions<BusinessRulesOptions> rules,
        IOptions<ReferenceListsOptions> referenceLists) : ICatalogSummaryService
    {
        /// <summary>10 mục của nhóm theo thứ tự D-121 / D-122.</summary>
        public static readonly (string Screen, string Name, string Section)[] Items =
        [
            ("adm-orgunit", "Công ty & đơn vị", "Tổ chức"), ("adm-position", "Chức vụ", "Tổ chức"), ("hr-shift", "Ca làm việc", "Tổ chức"),
            ("scm-uom", "Đơn vị tính", "Mua hàng & kho"), ("scm-warehouse", "Kho & vị trí", "Mua hàng & kho"),
            ("prod-line", "Dây chuyền & bàn nấu", "Sản xuất"), ("log-vehicle", "Xe giao hàng", "Giao hàng"),
            ("adm-reference", "Danh mục dùng chung", "Dùng chung"), ("adm-admin-unit", "Địa giới hành chính", "Dùng chung"), ("kpi-definition", "Danh mục chỉ số", "Dùng chung"),
        ];

        private static async Task<(DateTime? At, Guid? By)> LastAsync<T>(IQueryable<T> q) where T : BaseEntity
        {
            var row = await q.OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate).Select(x => new { At = x.ModifiedDate ?? x.CreatedDate, By = x.ModifiedBy ?? x.CreatedBy }).FirstOrDefaultAsync();
            return (row?.At, row?.By);
        }

        public async Task<IResults<CatalogCardModel>> GetCatalogSummary()
        {
            try
            {
                var db = unitOfWork.DbContext;
                var perms = await permissions.GetAsync(currentActor.RequireActor().UserId);
                var hr = rules.Value.Hr;
                var list = new List<CatalogCardModel>();
                var who = new Dictionary<CatalogCardModel, Guid?>();
                void W(CatalogCardModel c, string code, int n, string text, string screen)
                {
                    if (n > 0)
                    {
                        c.ListWarning.Add(new CatalogWarningModel { Code = code, Count = n, Text = string.Format(text, n), Screen = screen });
                    }
                }
                foreach (var (screen, name, section) in Items)
                {
                    if (!permissions.IsAllowed(perms, screen, PermissionAction.View))
                    {
                        continue;
                    }
                    var c = new CatalogCardModel { Screen = screen, Name = name, Section = section };
                    (DateTime? At, Guid? By) last = (null, null);
                    switch (screen)
                    {
                        case "adm-orgunit":
                            var units = db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted);
                            c.Count = await units.CountAsync(o => o.Status != "Inactive");
                            last = await LastAsync(units);
                            W(c, "NO_MANAGER", await units.CountAsync(o => o.Status != "Inactive" && o.ManagerId == null), "{0} đơn vị chưa có trưởng đơn vị", "adm-orgunit");
                            W(c, "EMP_NO_UNIT", await db.Employee.AsNoTracking().CountAsync(e => !e.IsDeleted && e.Status != EmployeeStatus.Resigned && e.OrganizationUnitId == null), "{0} nhân viên chưa gắn đơn vị", "adm-employee");
                            break;
                        case "adm-position":
                            var pos = db.Position.AsNoTracking().Where(p => !p.IsDeleted);
                            c.Count = await pos.CountAsync();
                            last = await LastAsync(pos);
                            var codes = await pos.Select(p => p.Code).ToListAsync();
                            W(c, "NO_GROUP", codes.Count(code => code == null || !rules.Value.PositionDefaultGroups.ContainsKey(code)), "{0} chức vụ chưa có nhóm quyền gợi ý", "adm-position");
                            break;
                        case "hr-shift":
                            var shifts = db.Shift.AsNoTracking().Where(s => !s.IsDeleted);
                            c.Count = await shifts.CountAsync(s => s.Status == null || s.Status == "Active");
                            last = await LastAsync(shifts);
                            var teamCodes = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted && o.Status != "Inactive" && db.Employee.Any(e => !e.IsDeleted && e.OrganizationUnitId == o.Id)).Select(o => o.Code).ToListAsync();
                            W(c, "NO_MIN_STAFF", teamCodes.Count(code => code == null || !hr.ShiftMinStaff.ContainsKey(code)), "{0} tổ chưa có số người tối thiểu cho ca", "hr-shift-assign");
                            break;
                        case "scm-uom":
                            var uoms = db.UnitMeasure.AsNoTracking().Where(u => !u.IsDeleted);
                            c.Count = await uoms.CountAsync();
                            last = await LastAsync(uoms);
                            var convs = (await db.UomConversion.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).Select(x => new { x.FromUnitId, x.ToUnitId }).ToListAsync())
                                .Select(x => (x.FromUnitId, x.ToUnitId)).ToHashSet();
                            var ings = await db.Ingredient.AsNoTracking().Where(i => !i.IsDeleted && i.BaseUnitId != null).Select(i => new { i.BaseUnitId, i.PurchaseUomId, i.IssueUomId }).ToListAsync();
                            bool Missing(Guid? u, Guid b) => u is { } x && x != b && !convs.Contains((x, b)) && !convs.Contains((b, x));
                            W(c, "NO_CONVERSION", ings.Count(i => Missing(i.PurchaseUomId, i.BaseUnitId!.Value) || Missing(i.IssueUomId, i.BaseUnitId!.Value)), "{0} nguyên liệu dùng đơn vị chưa có quy đổi về đơn vị chuẩn", "scm-uom");
                            break;
                        case "scm-warehouse":
                            var whs = db.Warehouse.AsNoTracking().Where(w => !w.IsDeleted);
                            c.Count = await whs.CountAsync();
                            last = await LastAsync(whs);
                            W(c, "ZONE_NO_TEMP", await db.StorageZone.AsNoTracking().CountAsync(z => !z.IsDeleted && z.TemperatureMin == null && z.TemperatureMax == null), "{0} khu bảo quản chưa có ngưỡng nhiệt độ", "scm-warehouse");
                            break;
                        case "prod-line":
                            var lines = db.ProductionLine.AsNoTracking().Where(l => !l.IsDeleted);
                            c.Count = await lines.CountAsync(l => l.Status == null || l.Status == "Active");
                            last = await LastAsync(lines);
                            W(c, "CAT_NO_LINE", await db.DishCategory.AsNoTracking().CountAsync(d => !d.IsDeleted && d.ProductionLineId == null), "{0} nhóm món chưa gán dây chuyền", "prod-line");
                            break;
                        case "log-vehicle":
                            var veh = db.Vehicle.AsNoTracking().Where(v => !v.IsDeleted);
                            c.Count = await veh.CountAsync(v => v.Status == null || v.Status == "Active");
                            last = await LastAsync(veh);
                            W(c, "NO_CAPACITY", await veh.CountAsync(v => (v.Status == null || v.Status == "Active") && (v.Capacity == null || v.Capacity <= 0)), "{0} xe đang hoạt động chưa có tải trọng", "log-vehicle");
                            break;
                        case "adm-reference":
                            c.Count = referenceLists.Value.Lists.Count;
                            break;
                        case "adm-admin-unit":
                            var au = db.AdministrativeUnit.AsNoTracking();
                            c.Count = await au.CountAsync();
                            break;
                        case "kpi-definition":
                            var kpi = db.KpiMetricDefinition.AsNoTracking().Where(k => !k.IsDeleted);
                            c.Count = await kpi.CountAsync();
                            last = await LastAsync(kpi);
                            break;
                    }
                    c.LastModified = last.At;
                    who[c] = last.By;
                    list.Add(c);
                }
                var names = await InfoPanelRules.UserNamesAsync(db, [.. who.Values]);
                foreach (var c in list)
                {
                    c.LastModifiedBy = names.NameOf(who[c]);
                }
                return Results<CatalogCardModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCatalogSummary: ", ex.ToString()));
                throw;
            }
        }
    }
}
