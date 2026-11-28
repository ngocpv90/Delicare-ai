using System.Text.Json.Nodes;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-12 (D-133 §7.6): agent chỉ ghi trực tiếp 96 / 97 / 98; muốn sửa / thêm ghi chú thư mục nghiệp vụ → <c>propose_note_update</c> – người có quyền Sửa thư mục duyệt
    /// (KnowledgeService.Save chạy dưới người duyệt: kiểm quyền thư mục, frontmatter, dữ liệu nhạy cảm, SOP không tự Hiệu lực – vẫn qua Duyệt SOP).
    /// </summary>
    public static partial class AgentCommandRegistry
    {
        private static IEnumerable<AgentCommandDef> KnowledgeCommands() =>
        [
            new AgentCommandDef
            {
                Code = "propose_note_update", Title = "Sửa / thêm ghi chú kho tri thức", Agents = ["VIVI", "WH", "KITCHEN", "QA", "SALES", "PROC", "LOG", "HR", "KNOW"],
                Screen = "knowledge", ToolScreen = string.Empty, Action = PermissionAction.View,
                Description = "Đề xuất SỬA (hoặc thêm mới) 1 ghi chú ở thư mục nghiệp vụ: đường dẫn + toàn bộ nội dung markdown mới (giữ frontmatter; source: human sau khi duyệt). "
                    + "Chủ thư mục xác nhận; SOP / quyết định sau đó vẫn phải Gửi duyệt SOP mới có hiệu lực.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"duong_dan\":{\"type\":\"string\",\"description\":\"vd 45 ATTP/SOP nhiệt độ lõi.md\"},\"noi_dung\":{\"type\":\"string\",\"description\":\"Toàn bộ markdown mới (có frontmatter)\"}," + Common + "},\"required\":[\"duong_dan\",\"noi_dung\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    string path = Need(a, "duong_dan", "đường dẫn ghi chú").Replace("\\", "/").Trim();
                    string content = Need(a, "noi_dung", "nội dung mới");
                    if (content.Length > 60000)
                    {
                        throw new AgentToolException("Nội dung quá dài (tối đa 60.000 ký tự)");
                    }
                    var folder = KnowledgeVault.FolderOfPath(path) ?? throw new AgentToolException("Đường dẫn phải nằm trong 1 thư mục của kho (vd 45 ATTP/…)");
                    if (folder.AgentWritable || folder.Code == KnowledgeVault.Bod)
                    {
                        throw new AgentToolException(folder.AgentWritable ? "Thư mục 96 / 97 / 98 ghi bằng ghi_nhat_ky" : "Không đề xuất vào thư mục BOD");
                    }
                    if (KnowledgeVault.ScanSensitive(content) is { } pii)
                    {
                        throw new AgentToolException($"Không đề xuất: {pii} (§7.5-8)");
                    }
                    var cur = await Svc<IKnowledgeService>(sp).ReadFor(new KnowledgeCaller(null, "KNOW", null), path);
                    string? hash = cur.ResponseCode == 200 ? cur.Data?.Hash : null;
                    string newText = KnowledgeService.SetMeta(content.Replace("\r\n", "\n"), "source", KnowledgeVault.HumanSource);
                    var parsed = KnowledgeVault.Parse(path, newText);
                    if (KnowledgeVault.ValidateFrontmatter(parsed) is { } fm)
                    {
                        throw new AgentToolException("Frontmatter: " + fm);
                    }
                    var pv = new AgentPreview
                    {
                        Summary = (hash == null ? "Thêm ghi chú " : "Sửa ghi chú ") + path, DocumentNo = parsed.Id, Counterparty = folder.Dir, MainValue = parsed.Meta.GetValueOrDefault("status"),
                        OpenLink = "#/knowledge?path=" + Uri.EscapeDataString(path),
                        LineHeaders = ["", "Hiện tại", "Đề xuất"],
                        Lines = [["Độ dài", cur.Data?.Body?.Length.ToString() ?? "(chưa có)", parsed.Body.Length.ToString()], ["Tiêu đề", cur.Data?.Title ?? "–", parsed.Title]],
                        Payload = new JsonObject { ["path"] = path, ["content"] = newText, ["prevHash"] = hash, ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                    if (parsed.Type is "sop" or "quyet-dinh" && parsed.Status == "hieu-luc")
                    {
                        pv.Warnings.Add("SOP / quyết định không tự Hiệu lực – sẽ lưu khi duyệt nếu đổi trạng thái về nháp, sau đó Gửi duyệt SOP");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IKnowledgeService>(sp).Save(new KnowledgeSaveRequest { Path = S(p, "path"), Content = S(p, "content"), PrevHash = p["prevHash"]?.GetValue<string?>() });
                    return r.ResponseCode == 200 ? (null, $"Đã lưu ghi chú {r.Data?.Path}") : (r.ResponseMessage ?? "Không lưu được ghi chú", null);
                },
            },
        ];
    }
}
