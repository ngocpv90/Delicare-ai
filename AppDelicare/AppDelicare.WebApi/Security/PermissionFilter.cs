using AppDelicare.Data.Share;
using Microsoft.Extensions.DependencyInjection;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace AppDelicare.WebApi.Security
{
    /// <summary>
    /// T07: kiểm tra <see cref="PermissionAttribute"/> cho mọi action cần đăng nhập. Không có quyền → 403
    ///.
    /// An toàn mặc định: action cần đăng nhập mà KHÔNG khai báo quyền → 403 (chỉ admin qua).
    /// </summary>
    public sealed class PermissionFilter(IPermissionService permissions, ICurrentActor currentActor) : IAsyncAuthorizationFilter
    {
        /// <summary>T45R.8 (D-39): API còn gọi được khi đăng nhập bằng mật khẩu tạm (ngoài ra 403 "Cần đổi mật khẩu").</summary>
        private static readonly HashSet<string> TempPasswordUserActions = new(StringComparer.OrdinalIgnoreCase) { "ChangePassword", "GetCurrent" };

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // T45R.8 (D-39): token có claim pwd_temp (đăng nhập bằng mật khẩu tạm) → chỉ User/ChangePassword, User/GetCurrent, Authentication/*.
            // Chặn TRƯỚC mọi ngoại lệ bên dưới (SkipPermission như GetProfile cũng bị chặn). Đã đổi mật khẩu (DB không còn PBKDF2T$) thì token dùng bình thường.
            var principal = context.HttpContext.User;
            if (principal.Identity?.IsAuthenticated == true
                && principal.HasClaim(c => c.Type == AppDelicare.Model.Helper.ConstantHelper.JwtClaimIdentifiers.PwdTemp)
                && !AllowedWithTempPassword(context.ActionDescriptor))
            {
                ActorInfo? tempActor = currentActor.Actor;
                var users = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                if (tempActor == null || await users.IsTemporaryPassword(tempActor.UserId))
                {
                    Log.Warning("Chặn {Path}: tài khoản {Actor} đang dùng mật khẩu tạm, cần đổi mật khẩu", context.HttpContext.Request.Path.Value, tempActor);
                    context.Result = new ObjectResult(new AppDelicare.Data.Repositories.Result<bool>
                    {
                        ResponseCode = StatusCodes.Status403Forbidden,
                        ResponseMessage = AppDelicare.Model.Helper.ConstantHelper.Message.MustChangePassword,
                        Data = false,
                    }) { StatusCode = StatusCodes.Status403Forbidden };
                    return;
                }
            }

            var metadata = context.ActionDescriptor.EndpointMetadata;
            // HR-ACC-1.4 / 1.7 (D-82 b, h): MỌI API đã đăng nhập (kể cả tự phục vụ – SkipPermission) kiểm tài khoản còn mở + loại INTERNAL ở mỗi request
            // (quyền cache theo user, xóa cache ngay khi khóa → token đang có vô hiệu ngay, không cần danh sách thu hồi token).
            if (principal.Identity?.IsAuthenticated == true && !metadata.OfType<IAllowAnonymous>().Any() && currentActor.Actor is { } who)
            {
                UserPermissions state = await permissions.GetAsync(who.UserId, context.HttpContext.RequestAborted);
                if (!state.Active)
                {
                    Log.Warning("Chặn {Path}: tài khoản {Actor} đã khóa / không còn", context.HttpContext.Request.Path.Value, who);
                    context.Result = Deny(StatusCodes.Status401Unauthorized, AppDelicare.Model.Helper.ConstantHelper.Message.AccountLocked);
                    return;
                }
                if (!AppDelicare.Services.Implement.Identity.PersonLink.IsInternal(state.AccountType))
                {
                    Log.Warning("Chặn {Path}: tài khoản {Actor} loại {Type} không dùng API nội bộ", context.HttpContext.Request.Path.Value, who, state.AccountType);
                    context.Result = Deny(StatusCodes.Status403Forbidden, AppDelicare.Model.Helper.ConstantHelper.Message.AccountNotInternal);
                    return;
                }
            }
            if (metadata.OfType<IAllowAnonymous>().Any() || metadata.OfType<SkipPermissionAttribute>().Any()
                || !metadata.OfType<IAuthorizeData>().Any())
            {
                return;
            }
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return; // UseAuthorization / JWT đã trả 401.
            }

            ActorInfo? actor = currentActor.Actor;
            if (actor == null)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            // Action-level khai báo sau cùng trong metadata → ưu tiên.
            PermissionAttribute? required = metadata.OfType<PermissionAttribute>().LastOrDefault();
            UserPermissions userPermissions = await permissions.GetAsync(actor.UserId, context.HttpContext.RequestAborted);
            bool allowed = required == null
                ? userPermissions.IsAdmin
                : permissions.IsAllowed(userPermissions, required.ScreenId, required.Action)
                  || required.OrActions.Any(a => permissions.IsAllowed(userPermissions, required.ScreenId, a))
                  || required.OrScreens.Any(s => permissions.IsAllowed(userPermissions, s, required.Action));
            if (allowed && required != null && required.AndScreens.Length != 0)
            {
                allowed = required.AndScreens.All(s => permissions.IsAllowed(userPermissions, s, required.Action));
            }
            if (!allowed)
            {
                Log.Warning("Từ chối quyền {Action} trên {Screen} cho {Actor} ({Path})",
                    required?.Action.ToString() ?? "(chưa khai báo)", required?.ScreenId ?? "-", actor, context.HttpContext.Request.Path.Value);
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }
        }

        private static ObjectResult Deny(int status, string message) => new(new AppDelicare.Data.Repositories.Result<bool>
        {
            ResponseCode = status,
            ResponseMessage = message,
            Data = false,
        }) { StatusCode = status };

        private static bool AllowedWithTempPassword(Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor descriptor)
            => descriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action
               && (string.Equals(action.ControllerName, "Authentication", StringComparison.OrdinalIgnoreCase)
                   || (string.Equals(action.ControllerName, "User", StringComparison.OrdinalIgnoreCase) && TempPasswordUserActions.Contains(action.ActionName)));
    }

    /// <summary>
    /// T07: gắn quyền tự động cho controller CRUD cũ (kế thừa <see cref="BaseController"/>) – không phải sửa 120 file sinh mã:
    /// màn hình theo <see cref="ScreenMap.ByClass"/> (tên controller = tên class entity), thao tác theo tên action.
    /// Action / controller đã khai báo <see cref="PermissionAttribute"/> hoặc <see cref="SkipPermissionAttribute"/> thì giữ nguyên.
    /// Action nghiệp vụ không khớp quy tắc tên (duyệt, khóa…) phải tự gắn <see cref="PermissionAttribute"/>.
    /// </summary>
    public sealed class PermissionConvention : IControllerModelConvention
    {
        /// <summary>Thao tác trên chính tài khoản đang đăng nhập.</summary>
        private static readonly HashSet<string> SelfService = new(StringComparer.OrdinalIgnoreCase) { "GetProfile", "UpdateProfile", "ChangePassword", "GetCurrent" };

        /// <summary>Nguy hiểm / leo thang quyền: chỉ admin (D-26; ResetPassword, GetPreview – spec 3.2 / 3.3: nhóm Quản trị hệ thống).</summary>
        private static readonly HashSet<string> AdminActions = new(StringComparer.OrdinalIgnoreCase) { "DeleteData", "SaveIsAdmin", "ResetPassword", "GetPreview" };

        /// <summary>
        /// D-78 (6): màn kỹ thuật chỉ admin / FIN-IT nhưng dữ liệu đọc ở màn khác – thao tác XEM được thêm bằng quyền xem các màn này
        /// (danh mục chỉ số: màn KPI ngày / tháng, bảng điều hành đọc tên + đơn vị chỉ số).
        /// </summary>
        private static readonly Dictionary<string, string[]> ReadAlsoScreens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["KpiMetricDefinition"] = ["kpi-daily", "kpi-monthly", "dash-exec"],
        };

        /// <summary>Controller không trùng tên bảng.</summary>
        private static readonly Dictionary<string, string> ControllerScreens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard"] = "dash-exec",
            ["BillingStatement"] = "crm-billing", // BIZ-3.2
        };

        public void Apply(ControllerModel controller)
        {
            if (!typeof(BaseController).IsAssignableFrom(controller.ControllerType))
            {
                return;
            }
            if (controller.Attributes.OfType<PermissionAttribute>().Any() || controller.Attributes.OfType<SkipPermissionAttribute>().Any())
            {
                return;
            }
            // MENU-4.3: bảng con (không còn mục menu riêng) → quyền màn cha.
            bool hasParent = ScreenMap.ParentByClass.TryGetValue(controller.ControllerName, out var parent);
            string screen = ControllerScreens.TryGetValue(controller.ControllerName, out var s) ? s
                : hasParent ? parent.Screen
                : ScreenMap.ByClass.TryGetValue(controller.ControllerName, out var mapped) ? mapped
                : ScreenMap.FallbackScreen;

            foreach (var action in controller.Actions)
            {
                if (action.Attributes.OfType<PermissionAttribute>().Any() || action.Attributes.OfType<SkipPermissionAttribute>().Any())
                {
                    continue;
                }
                object filter = SelfService.Contains(action.ActionName) ? new SkipPermissionAttribute()
                    : AdminActions.Contains(action.ActionName) ? new AdminOnlyAttribute()
                    : hasParent && parent.WriteAsEdit ? ChildPermission(screen, ActionOf(action))
                    : new PermissionAttribute(screen, ActionOf(action))
                    {
                        OrScreens = ActionOf(action) == PermissionAction.View && ReadAlsoScreens.TryGetValue(controller.ControllerName, out var also) ? also : [],
                    };
                foreach (var selector in action.Selectors)
                {
                    selector.EndpointMetadata.Add(filter);
                }
            }
        }

        /// <summary>MENU-4.3: ghi dòng con = sửa chứng từ cha (Thêm dòng: Sửa hoặc Thêm; Xóa dòng: Sửa hoặc Xóa).</summary>
        public static PermissionAttribute ChildPermission(string parentScreen, PermissionAction action) => action switch
        {
            PermissionAction.Add => new PermissionAttribute(parentScreen, PermissionAction.Edit) { OrActions = [PermissionAction.Add] },
            PermissionAction.Delete => new PermissionAttribute(parentScreen, PermissionAction.Edit) { OrActions = [PermissionAction.Delete] },
            _ => new PermissionAttribute(parentScreen, action),
        };

        public static PermissionAction ActionOf(ActionModel action)
        {
            string name = action.ActionName;
            if (name.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) return PermissionAction.View;
            if (name.StartsWith("Insert", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Create", StringComparison.OrdinalIgnoreCase)) return PermissionAction.Add;
            if (name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) return PermissionAction.Delete;
            if (name.StartsWith("Export", StringComparison.OrdinalIgnoreCase)) return PermissionAction.Export;
            if (name.StartsWith("Print", StringComparison.OrdinalIgnoreCase)) return PermissionAction.Print;
            if (name.StartsWith("Import", StringComparison.OrdinalIgnoreCase)) return PermissionAction.Import;
            // Update, UpdateResult, Save, SaveIsActive, ResetPassword…: sửa. Action GET lạ: xem.
            bool isGet = action.Selectors.SelectMany(s => s.ActionConstraints ?? []).OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
                .Any(c => c.HttpMethods.All(m => m == "GET"));
            return isGet && !name.StartsWith("Save", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Reset", StringComparison.OrdinalIgnoreCase)
                ? PermissionAction.View
                : PermissionAction.Edit;
        }
    }
}
