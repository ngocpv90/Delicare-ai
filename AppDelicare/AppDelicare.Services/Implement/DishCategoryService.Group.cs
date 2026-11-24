using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.21 (D-102): nhóm món quản lý ngay trong màn Món ăn (mục menu riêng ẩn) – 1 cấp (bỏ nhóm cha),
    /// tên bắt buộc + không trùng (không phân biệt hoa thường), không ngừng dùng khi còn món đang dùng; GetWithCount cho cột nhóm bên trái.
    /// Không có cột thứ tự → sắp theo tên (không kéo thả – không thêm cột).
    /// </summary>
    public partial class DishCategoryService
    {
        private async Task<string> CheckGroupAsync(DishCategoryRequest request, DishCategory? exist)
        {
            var db = _unitOfWork.DbContext;
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Tên nhóm món").Trim();
            }
            request.Name = request.Name.Trim();
            string lower = request.Name.ToLower();
            Guid self = exist?.Id ?? Guid.Empty;
            if (await db.DishCategory.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.Id != self && x.Name != null && x.Name.ToLower() == lower))
            {
                return string.Format(ConstantHelper.Message.GroupNameDuplicate, request.Name);
            }
            request.ParentId = null;
            if (exist != null && request.Status == "Inactive" && exist.Status != "Inactive")
            {
                Guid id = exist.Id;
                int used = await db.Dish.AsNoTracking().Where(x => !x.IsDeleted && (x.Status == null || x.Status == "Active")).Where(x => x.DishCategoryId == id || (x.DishCategoryId == null && x.CategoryId == id)).CountAsync();
                if (used > 0)
                {
                    return string.Format(ConstantHelper.Message.GroupInUse, used, "món");
                }
            }
            return string.Empty;
        }

        /// <summary>Nhóm + số món đang dùng / tổng (cột nhóm bên trái); nhóm ngừng dùng xếp cuối; kèm số món chưa phân nhóm.</summary>
        public async Task<IResult<CategoryCountListModel>> GetWithCount()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var groups = await db.DishCategory.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Code, x.Name, x.Status, x.ParentId }).ToListAsync();
                var items = await db.Dish.AsNoTracking().Where(x => !x.IsDeleted)
                    .Select(x => new { Group = x.DishCategoryId ?? x.CategoryId, Active = x.Status == null || x.Status == "Active" }).ToListAsync();
                var model = new CategoryCountListModel
                {
                    Ungrouped = items.Count(i => i.Active && (i.Group == null || !groups.Any(g => g.Id == i.Group))),
                    TotalActive = items.Count(i => i.Active),
                    HadParent = groups.Count(g => g.ParentId != null),
                };
                model.ListGroup = [.. groups.Select(g => new CategoryCountModel
                    {
                        Id = g.Id, Code = g.Code, Name = g.Name, Status = g.Status ?? "Active",
                        ActiveCount = items.Count(i => i.Group == g.Id && i.Active), TotalCount = items.Count(i => i.Group == g.Id),
                    })
                    .OrderBy(g => g.Status == "Inactive" ? 1 : 0).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)];
                return Result<CategoryCountListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWithCount: ", ex.ToString()));
                throw;
            }
        }
    }
}
