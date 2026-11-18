using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>
    /// BOT-1.1 (D-62): kho tin của kênh tin nhắn cho container <c>messaging</c> (API nội bộ <c>BotChannel/*</c>, khóa client) và màn quản trị web.
    /// <list type="bullet">
    /// <item><b>ReceiveUpdate</b>: lưu update thô, chống trùng <c>IdempotencyKey = TG:{bot}:{update_id}</c> bằng đọc khóa <c>UPDLOCK, HOLDLOCK</c> trong transaction
    /// (chưa có unique index – GAP-INT-02); ghi <c>IntegrationLog</c> IN.</item>
    /// <item><b>ClaimInbound / ClaimOutbound</b>: lấy việc bằng <c>READPAST, UPDLOCK, ROWLOCK</c> → 2 lần lấy song song không trùng dòng; dòng giữ quá
    /// <c>Messaging:LeaseMinutes</c> được lấy lại. Outbound: tôn trọng <c>notBefore</c> (GAP-INT-03) và giãn cách thử lại <c>Messaging:RetryDelaysMinutes</c>.</item>
    /// <item><b>CompleteInbound / MarkOutbound</b>: chỉ nhận dòng đang Processing / Sending; Blocked → định danh chuyển Blocked (người dùng chặn bot).</item>
    /// </list>
    /// API nội bộ ghi dưới tài khoản <c>system</c>. DB InMemory (test) không chạy SQL thô → dùng truy vấn EF tương đương (không song song).
    /// </summary>
    public class BotChannelService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration,
        IOptionsMonitor<MessagingOptions> options, INotificationService notifications) : IBotChannelService
    {
        private static readonly HashSet<string> CompleteStatuses = new(StringComparer.OrdinalIgnoreCase)
            { MessagingCode.Inbound.Processed, MessagingCode.Inbound.Failed, MessagingCode.Inbound.Ignored };
        private static readonly HashSet<string> MarkStatuses = new(StringComparer.OrdinalIgnoreCase)
            { MessagingCode.Outbound.Sent, MessagingCode.Outbound.Failed, MessagingCode.Outbound.Blocked };

        private MessagingOptions Options => options.CurrentValue;

        // ---------------------------------------------------------------- API nội bộ (container messaging)

        public async Task<IResult<BotUpdateResultModel>> ReceiveUpdate(BotUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BotCode) || request.UpdateId == null)
            {
                return Result<BotUpdateResultModel>.Fail(ConstantHelper.Message.BotUpdateInvalid);
            }
            if ((request.PayloadJson?.Length ?? 0) > Options.MaxUpdateLength)
            {
                return Result<BotUpdateResultModel>.Fail(string.Format(ConstantHelper.Message.BotUpdateTooLarge, Options.MaxUpdateLength));
            }
            string bot = request.BotCode.Trim().ToUpperInvariant();
            var db = unitOfWork.DbContext;
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            if (!bots.TryGetValue(bot, out Guid channelId))
            {
                return Result<BotUpdateResultModel>.Fail(string.Format(ConstantHelper.Message.BotUnknown, bot));
            }
            if (!TrySystemUser(out Guid systemUser))
            {
                return Result<BotUpdateResultModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            string key = $"TG:{bot}:{request.UpdateId}";
            DateTime now = clock.Now;
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var existing = await FindInboundLockedAsync(key);
                if (existing != null)
                {
                    AddLog(channelId, MessagingCode.Log.In, MessagingCode.Log.OpReceive, key, now, MessagingCode.Log.StatusDuplicate, null, request.PayloadJson);
                    await unitOfWork.IntegrationLogRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    unitOfWork.CommitTransaction();
                    return Result<BotUpdateResultModel>.Success(new BotUpdateResultModel { Id = existing.Value.Id, Duplicate = true, ProcessingStatus = existing.Value.Status },
                        ConstantHelper.Message.BotUpdateDuplicate);
                }
                var message = new InboundMessage
                {
                    Id = Guid.NewGuid(),
                    IntegrationChannelId = channelId,
                    ExternalMessageId = MessagingRules.Truncate(request.ExternalMessageId, 255),
                    ExternalConversationId = MessagingRules.Truncate(request.ExternalConversationId, 255),
                    ExternalUserId = MessagingRules.Truncate(request.ExternalUserId, 255),
                    MessageType = MessagingRules.Truncate(request.MessageType, 50),
                    PayloadJson = request.PayloadJson,
                    ReceivedAt = now,
                    ProcessingStatus = MessagingCode.Inbound.Received,
                    IdempotencyKey = key,
                    CorrelationId = MessagingRules.Truncate(currentActor.CorrelationId, 100),
                };
                unitOfWork.InboundMessageRepository.Add(message);
                AddLog(channelId, MessagingCode.Log.In, MessagingCode.Log.OpReceive, key, now, MessagingCode.Log.StatusSuccess, null, request.PayloadJson);
                await unitOfWork.InboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                unitOfWork.CommitTransaction();
                return Result<BotUpdateResultModel>.Success(new BotUpdateResultModel { Id = message.Id, ProcessingStatus = message.ProcessingStatus },
                    ConstantHelper.Message.BotUpdateReceived);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ReceiveUpdate: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResults<BotInboundItemModel>> ClaimInbound(BotClaimRequest request)
        {
            var db = unitOfWork.DbContext;
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            var channels = ChannelsFor(bots, request.BotCode);
            if (channels.Count == 0 || !TrySystemUser(out Guid systemUser))
            {
                return Results<BotInboundItemModel>.Success([], ConstantHelper.Message.GetAllSuccessfully);
            }
            int max = Math.Clamp(request.Max ?? Options.MaxClaim, 1, Math.Max(1, Options.MaxClaim));
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            DateTime now = clock.Now, lease = now.AddMinutes(-Math.Max(1, Options.LeaseMinutes));
            var botOf = bots.ToDictionary(kv => kv.Value, kv => kv.Key);
            List<InboundMessage> claimed;
            if (db.Database.IsRelational())
            {
                using var tran = await unitOfWork.OpenTransaction();
                // Trả dữ liệu ngay từ OUTPUT – không đọc lại bảng (lần đọc lại có thể chạm dòng người khác đang khóa).
                claimed = (await unitOfWork.Connection.QueryAsync<InboundMessage>("""
                    WITH c AS (
                        SELECT TOP (@max) m.*
                        FROM dbo.InboundMessage m WITH (READPAST, UPDLOCK, ROWLOCK)
                        WHERE m.IsDeleted = 0 AND m.IntegrationChannelId IN @channels
                          AND (m.ProcessingStatus = @received OR (m.ProcessingStatus = @processing AND m.ModifiedDate < @lease))
                        ORDER BY m.ReceivedAt, m.Id)
                    UPDATE c SET ProcessingStatus = @processing, ModifiedDate = @now, ModifiedBy = @by
                    OUTPUT inserted.Id, inserted.IntegrationChannelId, inserted.ExternalUserId, inserted.ExternalConversationId, inserted.ExternalMessageId,
                           inserted.MessageType, inserted.PayloadJson, inserted.ReceivedAt;
                    """, new { max, channels, received = MessagingCode.Inbound.Received, processing = MessagingCode.Inbound.Processing, lease, now, by = systemUser },
                    unitOfWork.Transaction)).ToList();
                unitOfWork.CommitTransaction();
            }
            else
            {
                var rows = await db.InboundMessage.Where(m => !m.IsDeleted && channels.Contains(m.IntegrationChannelId)
                        && (m.ProcessingStatus == MessagingCode.Inbound.Received || (m.ProcessingStatus == MessagingCode.Inbound.Processing && m.ModifiedDate < lease)))
                    .OrderBy(m => m.ReceivedAt).Take(max).ToListAsync();
                rows.ForEach(m => { m.ProcessingStatus = MessagingCode.Inbound.Processing; m.ModifiedDate = now; m.ModifiedBy = systemUser; });
                await db.SaveChangesAsync();
                claimed = rows;
            }
            var items = claimed.OrderBy(m => m.ReceivedAt).Select(m => new BotInboundItemModel
            {
                Id = m.Id, BotCode = botOf.GetValueOrDefault(m.IntegrationChannelId), ExternalUserId = m.ExternalUserId, ExternalConversationId = m.ExternalConversationId,
                ExternalMessageId = m.ExternalMessageId, MessageType = m.MessageType, PayloadJson = m.PayloadJson, ReceivedAt = m.ReceivedAt,
            }).ToList();
            return Results<BotInboundItemModel>.Success(items, ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<bool>> CompleteInbound(BotCompleteInboundRequest request)
        {
            if (request.Id == null || string.IsNullOrWhiteSpace(request.Status) || !CompleteStatuses.Contains(request.Status))
            {
                return Result<bool>.Fail(string.Format(ConstantHelper.Message.BotStatusInvalid, request.Status));
            }
            if (!TrySystemUser(out Guid systemUser))
            {
                return Result<bool>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            var message = await unitOfWork.DbContext.InboundMessage.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted);
            if (message == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (message.ProcessingStatus != MessagingCode.Inbound.Processing)
            {
                return Result<bool>.Fail(ConstantHelper.Message.BotMessageNotClaimed);
            }
            message.ProcessingStatus = CompleteStatuses.First(s => s.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
            message.ErrorMessage = MessagingRules.Truncate(request.Error, 2000);
            if (!string.IsNullOrWhiteSpace(request.CorrelationId))
            {
                message.CorrelationId = MessagingRules.Truncate(request.CorrelationId, 100);
            }
            unitOfWork.InboundMessageRepository.Update(message);
            await unitOfWork.InboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
        }

        public async Task<IResults<BotOutboundItemModel>> ClaimOutbound(BotClaimRequest request)
        {
            var db = unitOfWork.DbContext;
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            var channels = ChannelsFor(bots, request.BotCode);
            if (channels.Count == 0 || !TrySystemUser(out Guid systemUser))
            {
                return Results<BotOutboundItemModel>.Success([], ConstantHelper.Message.GetAllSuccessfully);
            }
            var o = Options;
            int max = Math.Clamp(request.Max ?? o.MaxClaim, 1, Math.Max(1, o.MaxClaim));
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            DateTime now = clock.Now, lease = now.AddMinutes(-Math.Max(1, o.LeaseMinutes));
            List<OutboundMessage> claimed;
            if (db.Database.IsRelational())
            {
                using var tran = await unitOfWork.OpenTransaction();
                claimed = (await unitOfWork.Connection.QueryAsync<OutboundMessage>("""
                    WITH c AS (
                        SELECT TOP (@max) m.*
                        FROM dbo.OutboundMessage m WITH (READPAST, UPDLOCK, ROWLOCK)
                        WHERE m.IsDeleted = 0 AND m.IntegrationChannelId IN @channels
                          AND (   m.MessageStatus = @queued
                               OR (m.MessageStatus = @failed AND m.RetryCount BETWEEN 1 AND @maxRetries AND m.QueuedAt <= @now)
                               OR (m.MessageStatus = @sending AND m.ModifiedDate < @lease))
                          AND (ISJSON(m.PayloadJson) = 0 OR JSON_VALUE(m.PayloadJson, '$.notBefore') IS NULL
                               OR TRY_CONVERT(datetime2, JSON_VALUE(m.PayloadJson, '$.notBefore'), 126) <= @now)
                        ORDER BY m.QueuedAt, m.Id)
                    UPDATE c SET MessageStatus = @sending, ModifiedDate = @now, ModifiedBy = @by
                    OUTPUT inserted.Id, inserted.IntegrationChannelId, inserted.RecipientExternalId, inserted.ExternalConversationId, inserted.MessageType,
                           inserted.PayloadJson, inserted.CorrelationId, inserted.ProviderMessageId, inserted.RetryCount, inserted.QueuedAt;
                    """, new
                    {
                        max, channels, queued = MessagingCode.Outbound.Queued, failed = MessagingCode.Outbound.Failed, sending = MessagingCode.Outbound.Sending,
                        maxRetries = o.EffectiveRetryDelays.Count, lease, now, by = systemUser,
                    }, unitOfWork.Transaction)).ToList();
                unitOfWork.CommitTransaction();
            }
            else
            {
                var candidates = await db.OutboundMessage.Where(m => !m.IsDeleted && channels.Contains(m.IntegrationChannelId)
                        && (m.MessageStatus == MessagingCode.Outbound.Queued || m.MessageStatus == MessagingCode.Outbound.Failed
                            || (m.MessageStatus == MessagingCode.Outbound.Sending && m.ModifiedDate < lease)))
                    .OrderBy(m => m.QueuedAt).ToListAsync();
                var rows = candidates.Where(m => m.MessageStatus != MessagingCode.Outbound.Failed
                        || (m.RetryCount >= 1 && m.RetryCount <= o.EffectiveRetryDelays.Count && m.QueuedAt <= now))
                    .Where(m => NotBefore(m.PayloadJson) is not { } nb || nb <= now)
                    .Take(max).ToList();
                rows.ForEach(m => { m.MessageStatus = MessagingCode.Outbound.Sending; m.ModifiedDate = now; m.ModifiedBy = systemUser; });
                await db.SaveChangesAsync();
                claimed = rows;
            }
            var botOf = bots.ToDictionary(kv => kv.Value, kv => kv.Key);
            return Results<BotOutboundItemModel>.Success(claimed.OrderBy(m => m.QueuedAt).Select(m => new BotOutboundItemModel
            {
                Id = m.Id, BotCode = botOf.GetValueOrDefault(m.IntegrationChannelId), RecipientExternalId = m.RecipientExternalId,
                ExternalConversationId = m.ExternalConversationId, MessageType = m.MessageType, PayloadJson = m.PayloadJson, CorrelationId = m.CorrelationId,
                ProviderMessageId = m.ProviderMessageId, RetryCount = m.RetryCount,
            }).ToList(), ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<bool>> MarkOutbound(BotMarkOutboundRequest request)
        {
            if (request.Id == null || string.IsNullOrWhiteSpace(request.Status) || !MarkStatuses.Contains(request.Status))
            {
                return Result<bool>.Fail(string.Format(ConstantHelper.Message.BotStatusInvalid, request.Status));
            }
            if (!TrySystemUser(out Guid systemUser))
            {
                return Result<bool>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            var db = unitOfWork.DbContext;
            var message = await db.OutboundMessage.FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted);
            if (message == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (message.MessageStatus != MessagingCode.Outbound.Sending)
            {
                return Result<bool>.Fail(ConstantHelper.Message.BotMessageNotClaimed);
            }
            DateTime now = clock.Now;
            string status = MarkStatuses.First(s => s.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
            message.MessageStatus = status;
            string logStatus = MessagingCode.Log.StatusSuccess;
            switch (status)
            {
                case MessagingCode.Outbound.Sent:
                    message.SentAt = now;
                    message.LastError = null;
                    if (!string.IsNullOrWhiteSpace(request.ProviderMessageId))
                    {
                        message.ProviderMessageId = MessagingRules.Truncate(request.ProviderMessageId, 255);
                    }
                    break;
                case MessagingCode.Outbound.Failed:
                    message.RetryCount += 1;
                    message.LastError = MessagingRules.Truncate(request.Error, 2000);
                    // QueuedAt = giờ được phép gửi lần kế (thời điểm tạo gốc vẫn ở CreatedDate). Hết lượt → giữ Failed, ClaimOutbound không lấy nữa.
                    if (MessagingRules.RetryDelayMinutes(Options, message.RetryCount) is { } wait)
                    {
                        message.QueuedAt = now.AddMinutes(wait);
                    }
                    logStatus = MessagingCode.Log.StatusFailed;
                    break;
                default: // Blocked: người dùng chặn bot → không thử lại, định danh Blocked.
                    message.LastError = MessagingRules.Truncate(request.Error, 2000);
                    logStatus = MessagingCode.Log.StatusBlocked;
                    var identities = await db.UserExternalIdentity.Where(i => !i.IsDeleted && i.IntegrationChannelId == message.IntegrationChannelId
                        && i.ExternalUserId == message.RecipientExternalId && i.IdentityStatus == MessagingCode.Identity.Active).ToListAsync();
                    foreach (var identity in identities)
                    {
                        identity.IdentityStatus = MessagingCode.Identity.Blocked;
                        unitOfWork.UserExternalIdentityRepository.Update(identity);
                    }
                    break;
            }
            unitOfWork.OutboundMessageRepository.Update(message);
            AddLog(message.IntegrationChannelId, MessagingCode.Log.Out, MessagingCode.Log.OpSend, message.IdempotencyKey, now, logStatus, request.HttpStatus,
                null, message.CorrelationId, request.Error);
            await unitOfWork.OutboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
        }

        // ---------------------------------------------------------------- màn quản trị web (Catalog 216–219)

        public async Task<IResult<bool>> ReprocessInbound(Guid id)
        {
            var message = await unitOfWork.DbContext.InboundMessage.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
            if (message == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (message.ProcessingStatus is not (MessagingCode.Inbound.Failed or MessagingCode.Inbound.Ignored))
            {
                return Result<bool>.Fail(ConstantHelper.Message.BotReprocessInvalid);
            }
            message.ProcessingStatus = MessagingCode.Inbound.Received;
            message.ErrorMessage = null;
            unitOfWork.InboundMessageRepository.Update(message);
            await unitOfWork.InboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
        }

        public async Task<IResult<bool>> RetryOutbound(Guid id)
        {
            var message = await unitOfWork.DbContext.OutboundMessage.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
            if (message == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (message.MessageStatus is not (MessagingCode.Outbound.Failed or MessagingCode.Outbound.Cancelled))
            {
                return Result<bool>.Fail(ConstantHelper.Message.BotRetryInvalid);
            }
            message.MessageStatus = MessagingCode.Outbound.Queued;
            message.RetryCount = 0;
            message.QueuedAt = clock.Now;
            unitOfWork.OutboundMessageRepository.Update(message);
            await unitOfWork.OutboundMessageRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
        }

        public async Task<IResult<OutboundEnqueueResultModel>> EnqueueManual(OutboundMessageEnqueueRequest request)
        {
            int maxLen = Options.MaxManualTextLength;
            string? text = request.Text?.Trim();
            if (string.IsNullOrEmpty(text) || text.Length > maxLen)
            {
                return Result<OutboundEnqueueResultModel>.Fail(string.Format(ConstantHelper.Message.BotTextRequired, maxLen));
            }
            if (request.UserId == null && request.EmployeeId == null)
            {
                return Result<OutboundEnqueueResultModel>.Fail(ConstantHelper.Message.BotRecipientRequired);
            }
            string bot = string.IsNullOrWhiteSpace(request.BotCode) ? MessagingCode.BotVanHanh : request.BotCode.Trim().ToUpperInvariant();
            var actor = currentActor.RequireActor();
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var ids = await notifications.EnqueueAsync(new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText,
                    BotCode = bot,
                    UserIds = request.UserId is { } u ? [u] : [],
                    EmployeeIds = request.EmployeeId is { } e ? [e] : [],
                    Data = new { text, sentBy = actor.UserId },
                });
                unitOfWork.CommitTransaction();
                if (ids.Count == 0)
                {
                    return Result<OutboundEnqueueResultModel>.Fail(string.Format(ConstantHelper.Message.BotRecipientNotLinked, bot));
                }
                return Result<OutboundEnqueueResultModel>.Success(new OutboundEnqueueResultModel { Queued = ids.Count, MessageIds = ids },
                    string.Format(ConstantHelper.Message.BotEnqueued, ids.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " EnqueueManual: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<ConversationResultModel>> GetConversation(ConversationSearchRequest request)
        {
            var db = unitOfWork.DbContext;
            var identities = db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted);
            if (request.IdentityId != null)
            {
                identities = identities.Where(i => i.Id == request.IdentityId);
            }
            else if (request.UserId != null)
            {
                identities = identities.Where(i => i.UserId == request.UserId);
            }
            else if (request.EmployeeId != null)
            {
                identities = identities.Where(i => i.EmployeeId == request.EmployeeId);
            }
            else
            {
                return Result<ConversationResultModel>.Fail(ConstantHelper.Message.BotConversationFilterRequired);
            }
            var bots = await (from b in db.BotConfiguration.AsNoTracking() where !b.IsDeleted select new { b.IntegrationChannelId, b.BotCode }).ToListAsync();
            var botOf = bots.GroupBy(b => b.IntegrationChannelId).ToDictionary(g => g.Key, g => g.First().BotCode);
            var pairs = (await identities.Select(i => new { i.IntegrationChannelId, i.ExternalUserId }).ToListAsync())
                .Where(p => string.IsNullOrWhiteSpace(request.BotCode) || string.Equals(botOf.GetValueOrDefault(p.IntegrationChannelId), request.BotCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (pairs.Count == 0)
            {
                return Result<ConversationResultModel>.Success(new ConversationResultModel(), ConstantHelper.Message.GetAllSuccessfully);
            }
            var channelIds = pairs.Select(p => p.IntegrationChannelId).Distinct().ToList();
            var externalIds = pairs.Select(p => p.ExternalUserId).Distinct().ToList();
            DateTime? from = request.From, to = request.To?.Date == request.To ? request.To?.AddDays(1) : request.To;
            var inbound = await db.InboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && channelIds.Contains(m.IntegrationChannelId) && m.ExternalUserId != null && externalIds.Contains(m.ExternalUserId)
                            && (from == null || m.ReceivedAt >= from) && (to == null || m.ReceivedAt < to))
                .Select(m => new ConversationItemModel
                {
                    Id = m.Id, Direction = MessagingCode.Log.In, At = m.ReceivedAt, ExternalUserId = m.ExternalUserId, MessageType = m.MessageType,
                    Status = m.ProcessingStatus, PayloadJson = m.PayloadJson, CorrelationId = m.CorrelationId, Error = m.ErrorMessage, BotCode = m.IntegrationChannelId.ToString(),
                }).ToListAsync();
            var outbound = await db.OutboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && channelIds.Contains(m.IntegrationChannelId) && m.RecipientExternalId != null && externalIds.Contains(m.RecipientExternalId)
                            && (from == null || m.QueuedAt >= from) && (to == null || m.QueuedAt < to))
                .Select(m => new ConversationItemModel
                {
                    Id = m.Id, Direction = MessagingCode.Log.Out, At = m.SentAt ?? m.QueuedAt, ExternalUserId = m.RecipientExternalId, MessageType = m.MessageType,
                    Status = m.MessageStatus, PayloadJson = m.PayloadJson, CorrelationId = m.CorrelationId, Error = m.LastError, BotCode = m.IntegrationChannelId.ToString(),
                }).ToListAsync();
            // Chỉ cặp (bot, người) của định danh – không lẫn người khác trùng ExternalUserId trên bot khác.
            var pairSet = pairs.Select(p => (p.IntegrationChannelId, p.ExternalUserId)).ToHashSet();
            var all = inbound.Concat(outbound)
                .Where(x => pairSet.Contains((Guid.Parse(x.BotCode!), x.ExternalUserId!)))
                .OrderByDescending(x => x.At).ThenBy(x => x.Direction).ToList();
            all.ForEach(x => x.BotCode = botOf.GetValueOrDefault(Guid.Parse(x.BotCode!)));
            int size = Math.Clamp(request.PageSize, 1, Math.Max(1, Options.ConversationMaxPageSize));
            int page = Math.Max(1, request.Page);
            return Result<ConversationResultModel>.Success(new ConversationResultModel
            {
                Total = all.Count,
                Items = all.Skip((page - 1) * size).Take(size).ToList(),
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>BOT-1.4: tin đã gửi (có message_id) cùng sự việc – messaging sửa đồng loạt thẻ ở mọi người nhận (đã duyệt / đã nhận xử lý).</summary>
        public async Task<IResults<BotCorrelatedMessageModel>> GetCorrelated(BotCorrelatedRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CorrelationId))
            {
                return Results<BotCorrelatedMessageModel>.Success([], ConstantHelper.Message.GetAllSuccessfully);
            }
            var db = unitOfWork.DbContext;
            var botOf = (await RecipientResolver.ActiveBotsAsync(db)).ToDictionary(kv => kv.Value, kv => kv.Key);
            string corr = request.CorrelationId.Trim();
            var rows = await db.OutboundMessage.AsNoTracking()
                .Where(m => !m.IsDeleted && m.CorrelationId == corr && m.MessageStatus == MessagingCode.Outbound.Sent && m.ProviderMessageId != null)
                .OrderBy(m => m.SentAt).Take(200).ToListAsync();
            return Results<BotCorrelatedMessageModel>.Success(rows.Where(m => botOf.ContainsKey(m.IntegrationChannelId)).Select(m => new BotCorrelatedMessageModel
            {
                Id = m.Id, BotCode = botOf[m.IntegrationChannelId], ExternalConversationId = m.ExternalConversationId ?? m.RecipientExternalId,
                ProviderMessageId = m.ProviderMessageId, MessageType = m.MessageType,
            }).ToList(), ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- nội bộ

        /// <summary>Đọc dòng cùng khóa với khóa phạm vi giữ tới hết transaction (SQL Server) – request thứ 2 chờ request 1 commit rồi thấy dòng đã có.</summary>
        private async Task<(Guid Id, string Status)?> FindInboundLockedAsync(string key)
        {
            var db = unitOfWork.DbContext;
            if (db.Database.IsRelational())
            {
                var row = await unitOfWork.Connection.QueryFirstOrDefaultAsync<LockedRow>("""
                    SELECT TOP (1) m.Id, m.ProcessingStatus AS Status FROM dbo.InboundMessage m WITH (UPDLOCK, HOLDLOCK)
                    WHERE m.IdempotencyKey = @key AND m.IsDeleted = 0
                    """, new { key }, unitOfWork.Transaction);
                return row == null ? null : (row.Id, row.Status);
            }
            var found = await db.InboundMessage.AsNoTracking().Where(m => m.IdempotencyKey == key && !m.IsDeleted)
                .Select(m => new { m.Id, m.ProcessingStatus }).FirstOrDefaultAsync();
            return found == null ? null : (found.Id, found.ProcessingStatus);
        }

        private sealed class LockedRow
        {
            public Guid Id { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private void AddLog(Guid channelId, string direction, string operation, string? key, DateTime now, string status, int? httpStatus,
            string? payload, string? correlationId = null, string? error = null)
        {
            unitOfWork.IntegrationLogRepository.Add(new IntegrationLog
            {
                Id = Guid.NewGuid(),
                IntegrationChannelId = channelId,
                DirectionCode = direction,
                OperationCode = operation,
                CorrelationId = MessagingRules.Truncate(correlationId ?? currentActor.CorrelationId, 100),
                IdempotencyKey = MessagingRules.Truncate(key, 200),
                RequestAt = now,
                ResponseAt = now,
                HttpStatus = httpStatus,
                LogStatus = status,
                ErrorMessage = MessagingRules.Truncate(error, 2000),
                PayloadHash = payload == null ? null : MessagingRules.PayloadHash(payload),
            });
        }

        private static List<Guid> ChannelsFor(Dictionary<string, Guid> bots, string? botCode)
            => string.IsNullOrWhiteSpace(botCode) ? bots.Values.Distinct().ToList()
                : bots.TryGetValue(botCode.Trim(), out Guid id) ? [id] : [];

        private bool TrySystemUser(out Guid systemUser)
            => Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out systemUser) && systemUser != Guid.Empty;

        private static DateTime? NotBefore(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                return doc.RootElement.TryGetProperty("notBefore", out var nb) && nb.ValueKind == System.Text.Json.JsonValueKind.String
                    && DateTime.TryParse(nb.GetString(), out var at) ? at : null;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }
}
