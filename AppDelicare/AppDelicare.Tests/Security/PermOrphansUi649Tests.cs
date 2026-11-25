using System.Reflection;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// UI-6.49 (8) (QĐ-71, D-127 g): quyền lẻ trên mục đã ẩn (docs/claude/PERM_ORPHANS.md) "không còn màn nào dùng" – khóa lại: không action API nào
    /// kiểm quyền trực tiếp theo mã mục đã ẩn (ScreenMap.HiddenScreenParent); API bảng con kiểm theo màn cha (ScreenMap.ParentByClass).
    /// </summary>
    public class PermOrphansUi649Tests
    {
        [Fact]
        public void No_api_action_checks_permission_on_a_hidden_screen()
        {
            var hidden = ScreenMap.HiddenScreenParent.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var offenders = typeof(PermissionAttribute).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<PermissionAttribute>().Concat(t.GetCustomAttributes<PermissionAttribute>())
                        .Select(a => (Where: t.Name + "." + m.Name, a))))
                .Where(x => x.a.Action != AppDelicare.Services.Interface.PermissionAction.Approve && (hidden.Contains(x.a.ScreenId) || x.a.AndScreens.Any(hidden.Contains))) // Duyệt kiểm theo mục #/wf-inbox?type=… (không ẩn)
                .Select(x => $"{x.Where} → {x.a.ScreenId}").Distinct().ToList();
            offenders.Should().BeEmpty("mục đã ẩn không còn được dùng để kiểm quyền API (PERM_ORPHANS.md)");
        }
    }
}
