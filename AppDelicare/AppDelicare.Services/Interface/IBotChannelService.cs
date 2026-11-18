using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-1.1 (D-62): kho tin kênh tin nhắn – API nội bộ cho container messaging + thao tác quản trị web (Catalog 216–219).</summary>
    public interface IBotChannelService
    {
        Task<IResult<BotUpdateResultModel>> ReceiveUpdate(BotUpdateRequest request);
        Task<IResults<BotInboundItemModel>> ClaimInbound(BotClaimRequest request);
        Task<IResult<bool>> CompleteInbound(BotCompleteInboundRequest request);
        Task<IResults<BotOutboundItemModel>> ClaimOutbound(BotClaimRequest request);
        Task<IResult<bool>> MarkOutbound(BotMarkOutboundRequest request);
        Task<IResult<bool>> ReprocessInbound(Guid id);
        Task<IResult<bool>> RetryOutbound(Guid id);
        Task<IResult<OutboundEnqueueResultModel>> EnqueueManual(OutboundMessageEnqueueRequest request);
        Task<IResult<ConversationResultModel>> GetConversation(ConversationSearchRequest request);
        Task<IResults<BotCorrelatedMessageModel>> GetCorrelated(BotCorrelatedRequest request);
    }
}
