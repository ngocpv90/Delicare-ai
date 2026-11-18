using AppDelicare.Messaging.Core.Contracts;

namespace AppDelicare.Messaging.Core
{
    /// <summary>BOT-1.3: bọc API nội bộ <c>api/BotChannel/*</c> (khóa client). Ném <see cref="ApiUnavailableException"/> khi api không trả lời.</summary>
    public interface IBotChannelApi
    {
        Task<ApiResult<BotUpdateResultModel>> ReceiveUpdateAsync(BotUpdateRequest request, CancellationToken ct = default);
        Task<List<BotInboundItemModel>> ClaimInboundAsync(string? botCode, int max, CancellationToken ct = default);
        Task CompleteInboundAsync(Guid id, string status, string? error = null, CancellationToken ct = default);
        Task<List<BotOutboundItemModel>> ClaimOutboundAsync(string? botCode, int max, CancellationToken ct = default);
        Task MarkOutboundAsync(BotMarkOutboundRequest request, CancellationToken ct = default);
        Task<ApiResult<BotIdentityModel>> ResolveIdentityAsync(BotIdentityRequest request, CancellationToken ct = default);
        Task<ApiResult<BotIdentityModel>> LinkByPhoneAsync(BotLinkByPhoneRequest request, CancellationToken ct = default);
        Task<ApiResult<BotIdentityModel>> LinkByCodeAsync(BotLinkByCodeRequest request, CancellationToken ct = default);
        Task<ApiResult<BotIdentityModel>> TouchIdentityAsync(BotTouchRequest request, CancellationToken ct = default);
        Task<ApiResult<bool>> UnlinkIdentityAsync(BotIdentityRequest request, CancellationToken ct = default);
        Task<ApiResult<UserTokenModel>> IssueUserTokenAsync(BotIdentityRequest request, CancellationToken ct = default);
        Task<ApiResult<MyAttendanceModel>> EmployeeGetTodayAsync(BotIdentityRequest request, CancellationToken ct = default);
        Task<List<BotCorrelatedMessageModel>> GetCorrelatedAsync(string correlationId, CancellationToken ct = default);
        Task<List<ReferenceItemModel>> GetLeaveTypesAsync(CancellationToken ct = default);
        Task<ApiResult<System.Text.Json.JsonElement>> EmployeeCallAsync(string action, object request, CancellationToken ct = default);
        Task<List<ShiftCardItemModel>> SyncShiftCardsAsync(CancellationToken ct = default);
        Task<ApiResult<ShiftCardItemModel>> MyShiftCardAsync(BotIdentityRequest request, CancellationToken ct = default);
        Task<ApiResult<IncidentResultModel>> IncidentActionAsync(IncidentActionRequest request, CancellationToken ct = default);
        Task<ApiResult<int>> RunEscalationAsync(CancellationToken ct = default);
    }

    public sealed class BotChannelApi(IDelicareApiClient api) : IBotChannelApi
    {
        public Task<ApiResult<BotUpdateResultModel>> ReceiveUpdateAsync(BotUpdateRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<BotUpdateResultModel>("ReceiveUpdate", request, ct);

        public async Task<List<BotInboundItemModel>> ClaimInboundAsync(string? botCode, int max, CancellationToken ct = default)
            => (await api.PostInternalAsync<BotInboundItemModel>("ClaimInbound", new BotClaimRequest { BotCode = botCode, Max = max }, ct)).ListData ?? [];

        public Task CompleteInboundAsync(Guid id, string status, string? error = null, CancellationToken ct = default)
            => api.PostInternalAsync<bool>("CompleteInbound", new BotCompleteInboundRequest { Id = id, Status = status, Error = error }, ct);

        public async Task<List<BotOutboundItemModel>> ClaimOutboundAsync(string? botCode, int max, CancellationToken ct = default)
            => (await api.PostInternalAsync<BotOutboundItemModel>("ClaimOutbound", new BotClaimRequest { BotCode = botCode, Max = max }, ct)).ListData ?? [];

        public Task MarkOutboundAsync(BotMarkOutboundRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<bool>("MarkOutbound", request, ct);

        public Task<ApiResult<BotIdentityModel>> ResolveIdentityAsync(BotIdentityRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<BotIdentityModel>("ResolveIdentity", request, ct);

        public Task<ApiResult<BotIdentityModel>> LinkByPhoneAsync(BotLinkByPhoneRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<BotIdentityModel>("LinkByPhone", request, ct);

        public Task<ApiResult<BotIdentityModel>> LinkByCodeAsync(BotLinkByCodeRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<BotIdentityModel>("LinkByCode", request, ct);

        public Task<ApiResult<BotIdentityModel>> TouchIdentityAsync(BotTouchRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<BotIdentityModel>("TouchIdentity", request, ct);

        public Task<ApiResult<bool>> UnlinkIdentityAsync(BotIdentityRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<bool>("UnlinkIdentity", request, ct);

        public Task<ApiResult<UserTokenModel>> IssueUserTokenAsync(BotIdentityRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<UserTokenModel>("IssueUserToken", request, ct);

        public Task<ApiResult<MyAttendanceModel>> EmployeeGetTodayAsync(BotIdentityRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<MyAttendanceModel>("EmployeeGetToday", request, ct);

        public async Task<List<ReferenceItemModel>> GetLeaveTypesAsync(CancellationToken ct = default)
            => (await api.PostInternalAsync<ReferenceItemModel>("GetLeaveTypes", new { }, ct)).ListData ?? [];

        public async Task<List<BotCorrelatedMessageModel>> GetCorrelatedAsync(string correlationId, CancellationToken ct = default)
            => (await api.PostInternalAsync<BotCorrelatedMessageModel>("GetCorrelated", new BotCorrelatedRequest { CorrelationId = correlationId }, ct)).ListData ?? [];

        /// <summary>BOT-1.4: action Employee* khác (EmployeeCheckIn / CheckOut / GetMyRequests / GetMyShifts) – kết quả thô.</summary>
        public Task<ApiResult<System.Text.Json.JsonElement>> EmployeeCallAsync(string action, object request, CancellationToken ct = default)
            => api.PostInternalAsync<System.Text.Json.JsonElement>(action, request, ct);

        /// <summary>BOT-2.1: xếp Thẻ ca tới hạn + thẻ đã gửi kèm nội dung mới nhất.</summary>
        public async Task<List<ShiftCardItemModel>> SyncShiftCardsAsync(CancellationToken ct = default)
            => (await api.PostInternalAsync<ShiftCardItemModel>("SyncShiftCards", new { }, ct)).ListData ?? [];

        public Task<ApiResult<ShiftCardItemModel>> MyShiftCardAsync(BotIdentityRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<ShiftCardItemModel>("MyShiftCard", request, ct);

        /// <summary>BOT-2.2: Nhận xử lý / Ghi chú / Đã xử lý; leo thang.</summary>
        public Task<ApiResult<IncidentResultModel>> IncidentActionAsync(IncidentActionRequest request, CancellationToken ct = default)
            => api.PostInternalAsync<IncidentResultModel>("IncidentAction", request, ct);

        public Task<ApiResult<int>> RunEscalationAsync(CancellationToken ct = default)
            => api.PostInternalAsync<int>("RunEscalation", new { }, ct);
    }
}
