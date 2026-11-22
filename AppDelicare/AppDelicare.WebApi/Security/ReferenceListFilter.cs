using AppDelicare.Data.Connection;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.WebApi.Security
{
    /// <summary>
    /// Lỗi UAT 2026-11-21 (rà soát danh mục): mọi action CRUD chung <c>Insert</c> / <c>Update</c> nhận <c>XxxRequest</c> – cột có trong
    /// <see cref="ReferenceCatalogs.Columns"/> (vd. Supplier.SupplierType, Warehouse.WarehouseType, MenuLine.MealType…) phải là mã của danh mục
    /// <c>ReferenceLists</c>. Nhận mã hoặc nhãn → ghi lại MÃ chuẩn vào request trước khi vào service; sai → 400 kiểu Result cũ, không ghi gì.
    /// Sửa bản ghi cũ có giá trị ngoài danh mục mà KHÔNG đổi cột đó → cho qua (đọc giá trị đang lưu).
    /// Action nghiệp vụ riêng (Contract/Create, MealWasteRecord/Record…) tự kiểm trong service.
    /// </summary>
    public sealed class ReferenceListFilter(IOptionsMonitor<ReferenceListsOptions> referenceLists) : IAsyncActionFilter
    {
        private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase) { "Insert", "Update" };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor is ControllerActionDescriptor action && Actions.Contains(action.ActionName))
            {
                foreach (var request in context.ActionArguments.Values.OfType<BaseRequest>())
                {
                    string? error = await CheckAsync(context, request, action.ActionName);
                    if (error != null)
                    {
                        context.Result = new OkObjectResult(Result<bool>.Fail(error));
                        return;
                    }
                }
            }
            await next();
        }

        private async Task<string?> CheckAsync(ActionExecutingContext context, BaseRequest request, string actionName)
        {
            string typeName = request.GetType().Name;
            if (!typeName.EndsWith("Request", StringComparison.Ordinal))
            {
                return null;
            }
            string entity = typeName[..^"Request".Length];
            object? stored = null;
            bool storedLoaded = false;
            foreach (var (column, _) in ReferenceCatalogs.Columns.Where(c => c.Key.StartsWith(entity + ".", StringComparison.OrdinalIgnoreCase)))
            {
                string prop = column[(entity.Length + 1)..];
                var pi = request.GetType().GetProperty(prop);
                if (pi == null || pi.PropertyType != typeof(string) || !pi.CanWrite)
                {
                    continue;
                }
                string? value = pi.GetValue(request) as string;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }
                var (code, error) = ReferenceCatalogs.CheckColumn(referenceLists.CurrentValue, column, value);
                if (error != null && string.Equals(actionName, "Update", StringComparison.OrdinalIgnoreCase) && request.Id != null)
                {
                    if (!storedLoaded)
                    {
                        stored = await LoadStoredAsync(context, entity, request.Id.Value);
                        storedLoaded = true;
                    }
                    string? old = stored?.GetType().GetProperty(prop)?.GetValue(stored) as string;
                    (code, error) = ReferenceCatalogs.CheckColumn(referenceLists.CurrentValue, column, value, old);
                }
                if (error != null)
                {
                    return error;
                }
                pi.SetValue(request, code);
            }
            return null;
        }

        /// <summary>Bản ghi đang lưu (không theo dõi) – để cho qua giá trị cũ ngoài danh mục khi không đổi.</summary>
        private static async Task<object?> LoadStoredAsync(ActionExecutingContext context, string entity, Guid id)
        {
            var db = context.HttpContext.RequestServices.GetService<DatabaseContext>();
            var type = db?.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == entity)?.ClrType;
            if (db == null || type == null)
            {
                return null;
            }
            var row = await db.FindAsync(type, id);
            if (row != null)
            {
                db.Entry(row).State = EntityState.Detached;
            }
            return row;
        }
    }
}
