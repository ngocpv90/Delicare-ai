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

        /// <summary>
        /// FIX-NOW-1 (D-92 b 5 – kiểm bằng vai trò không phải admin): màn của vai trò đọc nhãn từ bảng liên quan qua <c>GetAllData</c>
        /// (vd Thực đơn tuần đọc tên món / nhóm món, Phiếu xuất SX đọc lệnh SX) – trước đây 403 khi vai trò không có quyền Xem màn gốc của bảng đó.
        /// Chỉ action <c>GetAllData</c> (danh sách tra cứu, không phải màn danh sách / chi tiết / ghi) được mở thêm cho người Xem được một màn có bảng
        /// <b>liên quan trực tiếp</b> (quan hệ logic <see cref="AppDelicare.Data.Share.ReferenceMap"/>, 2 chiều; bảng con tính theo màn cha);
        /// danh mục gốc (<see cref="MasterClasses"/>) thêm 1 bậc (vd Thực đơn → dòng thực đơn → món → nhóm món). Tên người dùng (User) đọc được từ mọi màn
        /// (cột người tạo / phụ trách). Không đổi ma trận quyền màn hình (D-79).
        /// </summary>
        private static readonly HashSet<string> MasterClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dish", "DishCategory", "Ingredient", "IngredientCategory", "UnitMeasure", "UnitConversion", "Supplier", "Customer", "CustomerDeliveryLocation",
            "Warehouse", "StorageZone", "StorageLocation", "Shift", "Position", "OrganizationUnit", "Company", "ProductionLine", "KitchenWorkstation", "Vehicle",
            "Recipe", "Menu", "FoodSafetyChecklist", "ProductionStage", "Employee", "Person",
        };

        private static readonly Lazy<Dictionary<string, string[]>> LookupScreens = new(BuildLookupScreens);

        private static Dictionary<string, string[]> BuildLookupScreens()
        {
            var cmp = StringComparer.OrdinalIgnoreCase;
            var tableOf = AppDelicare.Data.Share.ReferenceMap.TableOfClass;
            var classOf = tableOf.GroupBy(kv => kv.Value, cmp).ToDictionary(g => g.Key, g => g.First().Key, cmp);
            var adj = new Dictionary<string, HashSet<string>>(cmp);
            void Link(string a, string b)
            {
                (adj.TryGetValue(a, out var x) ? x : adj[a] = new HashSet<string>(cmp)).Add(b);
                (adj.TryGetValue(b, out var y) ? y : adj[b] = new HashSet<string>(cmp)).Add(a);
            }
            foreach (var (target, sources) in AppDelicare.Data.Share.ReferenceMap.Incoming)
            {
                foreach (var (source, _) in sources)
                {
                    Link(target, source);
                }
            }
            string? ScreenOfTable(string table)
            {
                if (!classOf.TryGetValue(table, out string? cls))
                {
                    return null;
                }
                if (ScreenMap.ParentByClass.TryGetValue(cls, out var parent))
                {
                    return parent.Screen;
                }
                return ScreenMap.ByClass.TryGetValue(cls, out string? s) && s != ScreenMap.FallbackScreen && !s.StartsWith("dbx-", StringComparison.Ordinal) ? s : null;
            }
            var result = new Dictionary<string, string[]>(cmp);
            foreach (var (cls, table) in tableOf)
            {
                if (!adj.TryGetValue(table, out var hop1))
                {
                    continue;
                }
                var tables = new HashSet<string>(hop1, cmp);
                if (MasterClasses.Contains(cls))
                {
                    foreach (string t in hop1)
                    {
                        // Bảng "trung tâm" (công ty, đơn vị) nối gần như mọi bảng – không đi xuyên qua ở bậc 2.
                        if (t.Equals("Company", StringComparison.OrdinalIgnoreCase) || t.Equals("OrganizationUnit", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (adj.TryGetValue(t, out var hop2))
                        {
                            tables.UnionWith(hop2);
                        }
                    }
                }
                string? own = ScreenMap.ByClass.TryGetValue(cls, out string? o) ? o : null;
                result[cls] = [.. tables.Select(ScreenOfTable).OfType<string>().Where(s => s != own).Distinct().OrderBy(s => s, StringComparer.Ordinal)];
            }
            // Quan hệ 2 bậc qua chứng từ trung gian (quét theo vai trò trên DB dev): Kiểm tra khay hộp thu hồi → phiếu thu hồi → phiếu giao.
            foreach (var (cls, extra) in new (string, string[])[] { ("DeliveryOrder", ["log-return-inspection"]), ("MaterialReservation", ["scm-issue"]) })
            {
                result[cls] = [.. (result.TryGetValue(cls, out var cur) ? cur : []).Concat(extra).Distinct().OrderBy(s => s, StringComparer.Ordinal)];
            }
            // Tên người dùng (người tạo / phụ trách / người gửi) hiện ở mọi màn – tên lấy từ Person nên Person cũng vậy.
            result["User"] = result["Person"] = [.. ScreenMap.ByClass.Values.Where(s => s != ScreenMap.FallbackScreen && !s.StartsWith("dbx-", StringComparison.Ordinal) && s != "adm-user").Distinct().OrderBy(s => s, StringComparer.Ordinal)];
            return result;
        }

        /// <summary>Màn cho phép đọc <c>GetAllData</c> của bảng ngoài màn gốc (FIX-NOW-1) – dùng cho test.</summary>
        public static IReadOnlyList<string> LookupScreensOf(string controller) => LookupScreens.Value.TryGetValue(controller, out var s) ? s : [];

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
                bool lookup = string.Equals(action.ActionName, "GetAllData", StringComparison.OrdinalIgnoreCase);
                string[] lookupScreens = lookup ? [.. LookupScreensOf(controller.ControllerName)] : [];
                object filter = SelfService.Contains(action.ActionName) ? new SkipPermissionAttribute()
                    : AdminActions.Contains(action.ActionName) ? new AdminOnlyAttribute()
                    // FIX-NOW-1: bảng con cũng mở GetAllData cho màn có bảng liên quan (vd Đơn giao hàng đọc điểm dừng của chuyến).
                    : hasParent && parent.WriteAsEdit ? WithLookup(ChildPermission(screen, ActionOf(action)), lookupScreens)
                    : new PermissionAttribute(screen, ActionOf(action))
                    {
                        OrScreens = ActionOf(action) != PermissionAction.View ? []
                            : [.. (ReadAlsoScreens.TryGetValue(controller.ControllerName, out var also) ? also : []), .. lookupScreens],
                    };
                foreach (var selector in action.Selectors)
                {
                    selector.EndpointMetadata.Add(filter);
                }
            }
        }

        private static PermissionAttribute WithLookup(PermissionAttribute p, string[] screens)
        {
            return screens.Length == 0 || p.Action != PermissionAction.View ? p
                : new PermissionAttribute(p.ScreenId, p.Action) { OrActions = p.OrActions, OrScreens = [.. p.OrScreens, .. screens], AndScreens = p.AndScreens };
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
