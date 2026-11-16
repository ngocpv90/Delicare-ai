using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// Gắn lên lệnh ghi (POST tạo mới / lệnh trạng thái): header <c>Idempotency-Key</c> gửi lại thì không ghi lần 2 (Spec 1.2).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter));

    /// <summary>
    /// Lưu kết quả lệnh vào sổ <c>IntegrationLog</c> (cột <c>IdempotencyKey</c> có sẵn – PROPOSED, ROADMAP T03):
    /// <list type="bullet">
    /// <item>Không gửi header → chạy bình thường.</item>
    /// <item>Đã có dòng thành công cùng key + thao tác + người gửi: cùng nội dung → KHÔNG chạy lại, trả lại mã HTTP cũ,
    /// header <c>Idempotency-Replayed: true</c>; khác nội dung → 422.</item>
    /// <item>Lệnh thất bại không được lưu → client được thử lại cùng key.</item>
    /// </list>
    /// IntegrationLog là sổ chỉ ghi thêm nên chỉ INSERT 1 dòng sau khi lệnh chạy xong (không có trạng thái "đang chạy" trong DB).
    /// Hai request cùng key chạy song song được chặn bằng khóa trong tiến trình (1 instance). Chạy nhiều instance: cần
    /// <c>sp_getapplock</c> – GAP-IDEM-02. Bảng không có cột lưu body → lần gửi lại không trả lại <c>data</c> (GAP-IDEM-01).
    /// </summary>
    public sealed class IdempotencyFilter(DatabaseContext db, ICurrentActor currentActor) : IAsyncActionFilter
    {
        public const string HeaderName = "Idempotency-Key";
        public const string ReplayedHeader = "Idempotency-Replayed";
        public const string Direction = "INBOUND";
        public const string StatusSucceeded = "Succeeded";
        private const int MaxKeyLength = 200;
        private const int MaxOperationLength = 100;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            string key = context.HttpContext.Request.Headers[HeaderName].ToString().Trim();
            if (string.IsNullOrEmpty(key))
            {
                await next();
                return;
            }
            if (key.Length > MaxKeyLength)
            {
                throw ApiException.Validation(HeaderName, $"Idempotency-Key tối đa {MaxKeyLength} ký tự.");
            }

            ActorInfo actor = currentActor.RequireActor();
            string operation = OperationOf(context.HttpContext.Request);
            string payloadHash = HashOf(context.ActionArguments);
            string lockKey = $"{actor.UserId:N}|{operation}|{key}";
            SemaphoreSlim gate = Locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await gate.WaitAsync(context.HttpContext.RequestAborted);
            try
            {
                var previous = await db.IntegrationLog.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.DirectionCode == Direction && x.IdempotencyKey == key
                        && x.OperationCode == operation && x.CreatedBy == actor.UserId && x.LogStatus == StatusSucceeded)
                    .OrderByDescending(x => x.CreatedDate)
                    .Select(x => new { x.PayloadHash, x.HttpStatus, x.CorrelationId })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (previous != null)
                {
                    if (!string.Equals(previous.PayloadHash, payloadHash, StringComparison.Ordinal))
                    {
                        throw ApiException.Unprocessable(ErrorCodes.BusinessRule,
                            "Idempotency-Key này đã dùng cho một yêu cầu có nội dung khác. Hãy dùng key mới.", HeaderName);
                    }
                    context.HttpContext.Response.Headers[ReplayedHeader] = "true";
                    context.Result = new ObjectResult(new { replayed = true, originalTraceId = previous.CorrelationId })
                    {
                        StatusCode = previous.HttpStatus ?? StatusCodes.Status200OK,
                    };
                    return;
                }

                DateTime requestAt = DateTime.Now;
                // Lệnh + dòng IntegrationLog trong cùng 1 transaction: lệnh lỗi hoặc ghi log lỗi thì không ghi gì (SQL Server).
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(context.HttpContext.RequestAborted)
                    : null;
                ActionExecutedContext executed = await next();
                int status = StatusOf(executed);
                if (executed.Exception == null && status is >= 200 and < 300)
                {
                    db.IntegrationLog.Add(new IntegrationLog
                    {
                        Id = Guid.NewGuid(),
                        DirectionCode = Direction,
                        OperationCode = operation,
                        CorrelationId = Truncate(currentActor.CorrelationId, 100),
                        IdempotencyKey = key,
                        PayloadHash = payloadHash,
                        LogStatus = StatusSucceeded,
                        HttpStatus = status,
                        RequestAt = requestAt,
                        ResponseAt = DateTime.Now,
                        CreatedDate = DateTime.Now,
                        CreatedBy = actor.UserId,
                        IsDeleted = false,
                    });
                    await db.SaveChangesAsync(context.HttpContext.RequestAborted);
                    if (transaction != null)
                    {
                        await transaction.CommitAsync(context.HttpContext.RequestAborted);
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        }

        private static int StatusOf(ActionExecutedContext executed) => executed.Result switch
        {
            ObjectResult o => o.StatusCode ?? StatusCodes.Status200OK,
            StatusCodeResult s => s.StatusCode,
            _ => executed.HttpContext.Response.StatusCode,
        };

        /// <summary>Vd. <c>POST /api/v1/hr/shifts</c>; dài quá 100 ký tự thì rút gọn bằng hash.</summary>
        public static string OperationOf(HttpRequest request)
        {
            string op = $"{request.Method} {request.Path.Value}".ToLowerInvariant();
            return op.Length <= MaxOperationLength ? op : op[..60] + "#" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(op)))[..32];
        }

        private static string HashOf(IDictionary<string, object?> arguments)
        {
            var filtered = arguments.Where(a => a.Value is not CancellationToken).OrderBy(a => a.Key, StringComparer.Ordinal)
                .ToDictionary(a => a.Key, a => a.Value);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(filtered, ApiJson.Options);
            return Convert.ToHexString(SHA256.HashData(json));
        }

        private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
    }
}
