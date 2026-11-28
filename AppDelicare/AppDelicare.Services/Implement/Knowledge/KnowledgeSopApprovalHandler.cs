using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Services.Implement.Knowledge
{
    /// <summary>
    /// AI-12 (D-133): loại duyệt "Duyệt SOP" (BusinessType <c>KB_SOP</c>, mục <c>#/wf-inbox?type=kb-sop</c>). BusinessId = khóa đường dẫn ghi chú; nội dung được duyệt = mã băm tệp
    /// (sửa sau khi gửi → không duyệt được). Duyệt: người duyệt còn phải có quyền Sửa thư mục của ghi chú. IKnowledgeService lấy muộn (tránh vòng phụ thuộc với bộ máy duyệt).
    /// </summary>
    public sealed class KnowledgeSopApprovalHandler(IServiceProvider services) : IApprovalHandler
    {
        private IKnowledgeService K => services.GetRequiredService<IKnowledgeService>();

        public string BusinessType => KnowledgeService.SopBusinessType;

        public string ScreenId => KnowledgeService.SopScreen;

        public Task<string?> BuildPayloadAsync(Guid businessId) => K.SopStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId)
        {
            var (path, title, _, _) = K.SopInfo(businessId);
            return Task.FromResult<string?>(path == null ? null : $"Duyệt SOP: {title} ({path})");
        }

        public Task<ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx)
        {
            var (path, title, folder, status) = K.SopInfo(businessId);
            if (path == null)
            {
                return Task.FromResult<ApprovalSummaryModel?>(null);
            }
            var m = new ApprovalSummaryModel
            {
                Title = title, TypeName = "Duyệt SOP", ModuleName = "Kho tri thức", DocumentNo = Path.GetFileNameWithoutExtension(path), Counterparty = KnowledgeVault.FolderByCode(folder)?.Dir,
                MainValue = status, Reason = "Bản mới có hiệu lực khi duyệt; bản Hiệu lực cũ cùng mã chuyển Lưu trữ",
            };
            return Task.FromResult<ApprovalSummaryModel?>(m);
        }

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => K.ApplySopDecisionAsync(businessId, true, actorUserId, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => K.ApplySopDecisionAsync(businessId, false, actorUserId, note);
    }
}
