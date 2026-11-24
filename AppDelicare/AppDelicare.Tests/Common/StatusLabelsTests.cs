using System.Text.RegularExpressions;
using AppDelicare.Model.Helper;
using FluentAssertions;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// UI-6.10 (D-88 c): từ điển trạng thái chung – máy chủ (<see cref="StatusLabels"/>) và giao diện (<c>UI.STATUS_VI</c> trong core/ui.js) là 2 nguồn
    /// trùng nhau → test so khớp (DoD D-92 b 4). Active / Inactive một cách gọi duy nhất ở mọi bảng của danh mục trạng thái hiệu lực.
    /// </summary>
    public class StatusLabelsTests
    {
        private static string RepoPath(params string[] parts)
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine([dir!, .. parts]);
        }

        [Fact]
        public void Server_and_ui_dictionaries_agree()
        {
            string js = File.ReadAllText(RepoPath("app-delicare-client", "src", "assets", "delicare", "js", "core", "ui.js"));
            int start = js.IndexOf("UI.STATUS_VI = {", StringComparison.Ordinal);
            start.Should().BeGreaterThan(0);
            string body = js[start..js.IndexOf("};", start, StringComparison.Ordinal)];
            var ui = Regex.Matches(body, @"(\w+): '([^']+)'").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            ui.Should().NotBeEmpty();
            foreach (var (code, label) in ui.Where(kv => kv.Key is not ("true" or "false" or "IN" or "OUT")))
            {
                StatusLabels.Common.Should().ContainKey(code, $"mã {code} có ở giao diện thì máy chủ cũng phải có");
                StatusLabels.Common[code].Should().Be(label, $"nhãn của {code} phải giống nhau ở máy chủ và giao diện");
            }
        }

        [Fact]
        public void Active_and_inactive_have_one_name_everywhere()
        {
            foreach (var item in StatusCatalogEffective.All)
            {
                for (int i = 0; i < item.Codes.Length; i++)
                {
                    string label = i < item.Labels.Length ? item.Labels[i] : item.Codes[i];
                    new[] { "Hoạt động", "Đang áp dụng", "Ngừng", "Không hoạt động" }.Should().NotContain(label, $"{item.Key} = {item.Codes[i]} (D-88: Đang hoạt động / Ngừng hoạt động)");
                }
            }
            StatusLabels.Vi("Active").Should().Be("Đang hoạt động");
            StatusLabels.Vi("Inactive").Should().Be("Ngừng hoạt động");
            StatusLabels.Vi("HOP_GIU_NHIET").Should().Be("HOP_GIU_NHIET", "mã không có trong từ điển giữ nguyên (mã định danh)");
        }
    }
}
