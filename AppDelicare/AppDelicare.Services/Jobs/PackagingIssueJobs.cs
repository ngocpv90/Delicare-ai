using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// ACC-6 (D-131 j): phiếu xuất bao bì Nháp (thẻ [XUAT_BB:…]) của ca đã hết giờ → tự ghi sổ (cấu hình <c>BusinessRules:PackagingIssueAutoPost</c>).
    /// Kho đã sửa phiếu vẫn được ghi sổ theo số đã sửa; ghi sổ lỗi (thiếu tồn…) → bot báo Thủ kho. Chạy lại = 0 phiếu (chỉ còn Nháp mới ghi).
    /// </summary>
    public sealed class PackagingIssueAutoPostJob(IPackagingNormService norms) : IScheduledJob
    {
        public string Code => "PackagingIssueAutoPost";
        public string Name => "Tự ghi sổ phiếu xuất bao bì khi hết ca";
        public string Description => "Phiếu xuất bao bì Nháp (lập khi ghi kết quả đóng gói) của ca đã hết giờ → ghi sổ; lỗi thì báo Thủ kho.";
        public string DefaultAt => "23:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var (posted, failed) = await norms.AutoPostAsync(now);
            return new JobOutcome(failed.Count == 0 ? JobRunStatus.Succeeded : JobRunStatus.Failed, posted, now.Date,
                $"Ghi sổ {posted} phiếu bao bì" + (failed.Count == 0 ? string.Empty : $"; {failed.Count} lỗi: " + string.Join("; ", failed.Take(3))));
        }
    }
}
