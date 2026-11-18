using AppDelicare.Data.Interface;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-2.3: kiểm tra định kỳ (mẻ trễ, xe chưa rời, điểm trễ, chưa vào ca, phản ánh sắp quá SLA, PO trễ, duyệt quá hạn) → xếp tin vào outbox.</summary>
    public interface IBotWatchService
    {
        Task<IResult<int>> Run();
    }
}
