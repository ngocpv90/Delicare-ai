namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// MENU-TAB (anh 2026-11-28): mục quyền (ObjectMenu) là <b>tab của một màn</b>, không phải menu – giữ dòng quyền riêng (vd tab Sử dụng &amp; chi phí chỉ CEO / COO / FIN-IT)
    /// nhưng không tính là "màn hình" (số màn của nhóm / người dùng) và tìm kiếm toàn cục hiện "Màn cha › Tab". Khớp FE <c>Store.tabOf</c> (core/store.js).
    /// </summary>
    public static class MenuTabs
    {
        /// <summary>MetaLink của tab → (MetaLink màn cha, tên màn cha).</summary>
        public static readonly IReadOnlyDictionary<string, (string Parent, string ParentName)> ParentOf = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            // AI-21 (D-138 b): tab của Trung tâm Agent.
            ["#/ai-tasks"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-flow"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-agent-log"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-schedule"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-cost"] = ("#/ai-agents", "Trung tâm Agent"),
            // AI-25.4 (D-143): mục quyền nhận bản tin – nằm dưới Trung tâm Agent ở màn phân quyền (không mở quyền vào Trung tâm Agent).
            ["#/ai-ban-tin-sang"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-ban-tin-chieu"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-ban-tin-kho"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-ban-tin-bep"] = ("#/ai-agents", "Trung tâm Agent"),
            ["#/ai-ban-tin-kd"] = ("#/ai-agents", "Trung tâm Agent"),
            // UI-6.52 (D-139 a) / RPT-1: tab của Bảng điều hành.
            ["#/kpi-daily"] = ("#/dash-exec", "Bảng điều hành"),
            ["#/kpi-monthly"] = ("#/dash-exec", "Bảng điều hành"),
            ["#/cost-meal"] = ("#/dash-exec", "Bảng điều hành"),
            ["#/dash-pl"] = ("#/dash-exec", "Bảng điều hành"),
        };

        public static bool IsTab(string? metaLink) => metaLink != null && ParentOf.ContainsKey(metaLink.Split('?')[0]);
    }
}
