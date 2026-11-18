using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-1.1 (D-62): outbox – gọi TRONG transaction của nghiệp vụ; trả Id các OutboundMessage đã xếp (người chưa liên kết bot → bỏ qua).</summary>
    public interface INotificationService
    {
        Task<List<Guid>> EnqueueAsync(NotificationRequest request, CancellationToken ct = default);
    }
}
