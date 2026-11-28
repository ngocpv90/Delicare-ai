using AppDelicare.Messaging.Core.Contracts;

namespace AppDelicare.Messaging.Audiences.Internal
{
    /// <summary>Nguồn chức năng (mỗi nhóm chức năng 1 provider – Chung, VanHanh/&lt;phân hệ&gt;, DieuHanh/…).</summary>
    public interface IBotActionProvider
    {
        IEnumerable<BotAction> GetActions();
    }

    /// <summary>
    /// BOT-1.4: danh mục chức năng của bot + phân hệ. Chức năng hiện ra theo bot và quyền của người dùng (<see cref="BotAction.IsAllowed"/>).
    /// Phân hệ dùng cho 📂 Chức năng (MenuNavigator). BOT-1.5 / 1.6 thêm provider VanHanh / DieuHanh.
    /// </summary>
    public sealed class BotActionRegistry
    {
        /// <summary>Mã phân hệ → tên hiện trên nút (thứ tự = thứ tự hiển thị).</summary>
        public static readonly IReadOnlyList<(string Code, string Label)> Modules =
        [
            ("CHUNG", "👤 Cá nhân"),
            ("DUYET", "⏳ Phê duyệt"),
            ("HOINHANH", "💬 Hỏi nhanh"),
            ("BEP", "🍳 Bếp"),
            ("ATTP", "🧽 ATTP / QC"),
            ("GIAOVAN", "🚚 Giao vận"),
            ("DIEMPHAT", "🍱 Điểm phát"),
            ("KHO", "📦 Kho & mua hàng"),
            ("KEHOACH", "📋 Kế hoạch"),
            ("KINHDOANH", "💼 Kinh doanh"),
            ("CSKH", "📣 CSKH"),
            ("KETOAN", "🧾 Kế toán"),
            ("NHANSU", "👥 Nhân sự"),
            ("QUANTRI", "🖥 Quản trị"),
            ("DIEUHANH", "📊 Điều hành"),
        ];

        private readonly List<BotAction> _actions;

        public BotActionRegistry(IEnumerable<IBotActionProvider> providers)
        {
            _actions = [.. providers.SelectMany(p => p.GetActions()).OrderBy(a => ModuleOrder(a.Module)).ThenBy(a => a.Order)];
            var dup = _actions.GroupBy(a => a.Code).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
            {
                throw new InvalidOperationException("Trùng mã chức năng bot: " + dup.Key);
            }
        }

        public IReadOnlyList<BotAction> All => _actions;

        public List<BotAction> Available(string bot, BotIdentityModel identity) => [.. _actions.Where(a => a.IsAllowed(bot, identity))];

        /// <summary>BOT-UX-2: chức năng đã nằm sẵn trên bàn phím dưới (⚡ Ca của tôi, 📍 Chấm công) – không lặp lại trong 📂 menu.</summary>
        public static readonly IReadOnlySet<string> KeyboardCodes = new HashSet<string>(StringComparer.Ordinal) { "my_shift", "check_in" };

        /// <summary>BOT-UX-2: chức năng hiện trong 📂 menu (bỏ chức năng của bàn phím dưới) – đếm để quyết định màn đầu phẳng (≤ 6).</summary>
        public List<BotAction> MenuAvailable(string bot, BotIdentityModel identity) => [.. Available(bot, identity).Where(a => !KeyboardCodes.Contains(a.Code))];

        /// <summary>
        /// BOT-UX-2 (mục 14): phân hệ của người dùng theo thứ tự hiển thị – phân hệ của vai trò chính (nhiều chức năng cần mục quyền <c>identity.Menus</c> nhất) xếp đầu,
        /// còn lại theo thứ tự chuẩn. Admin toàn quyền giữ thứ tự chuẩn.
        /// </summary>
        public List<string> ModulesFor(string bot, BotIdentityModel identity)
        {
            var menu = MenuAvailable(bot, identity);
            var modules = menu.Select(a => a.Module).Distinct().ToList();
            if (identity.IsAdmin)
            {
                return modules;
            }
            int Weight(string m) => menu.Count(a => a.Module == m && a.MenuMetaLink != null && identity.Menus.Contains(a.MenuMetaLink, StringComparer.OrdinalIgnoreCase));
            string? main = modules.Select((m, i) => (m, i, w: Weight(m))).Where(x => x.w > 0).OrderByDescending(x => x.w).ThenBy(x => x.i).Select(x => x.m).FirstOrDefault();
            return main == null ? modules : [main, .. modules.Where(m => m != main)];
        }

        public BotAction? ByCode(string? code) => _actions.FirstOrDefault(a => a.Code == code);

        public BotAction? ByLabel(string bot, BotIdentityModel identity, string? text)
            => text == null ? null : Available(bot, identity).FirstOrDefault(a => string.Equals(a.Label, text.Trim(), StringComparison.Ordinal));

        public BotAction? ByCommand(string bot, BotIdentityModel identity, string? command)
            => command == null ? null : Available(bot, identity).FirstOrDefault(a => string.Equals(a.Command, command, StringComparison.OrdinalIgnoreCase));

        public static string ModuleLabel(string code) => Modules.FirstOrDefault(m => m.Code == code).Label ?? code;

        private static int ModuleOrder(string code)
        {
            for (int i = 0; i < Modules.Count; i++)
            {
                if (Modules[i].Code == code)
                {
                    return i;
                }
            }
            return Modules.Count;
        }
    }
}
