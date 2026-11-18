using System.Collections.Concurrent;
using AppDelicare.Messaging.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Processing
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §4): giữ quyết định rủi ro thấp <c>Sending:UndoSeconds</c> (10″) rồi mới gọi api. Bấm [↩️ Hoàn tác] trong thời gian đó → hủy, KHÔNG gọi api.
    /// Mỗi khóa (vd. người + yêu cầu duyệt) chỉ 1 việc đang chờ: bấm duyệt 2 lần → lần 2 bị bỏ (1 quyết định). Chỉ trong bộ nhớ – container dừng giữa chừng
    /// thì việc chưa chạy coi như hoàn tác (an toàn: api chưa ghi gì).
    /// </summary>
    public sealed class UndoWindow(IOptionsMonitor<SendingOptions> options, TimeProvider time, ILogger<UndoWindow> logger)
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

        public TimeSpan Delay => TimeSpan.FromSeconds(Math.Max(0, options.CurrentValue.UndoSeconds));

        public bool IsPending(string key) => _pending.ContainsKey(key);

        /// <summary>false = khóa này đang chờ (bấm lặp) – không lên lịch thêm.</summary>
        public bool Schedule(string key, Func<CancellationToken, Task> action)
        {
            var cts = new CancellationTokenSource();
            if (!_pending.TryAdd(key, cts))
            {
                cts.Dispose();
                return false;
            }
            _ = RunAsync(key, cts, action);
            return true;
        }

        /// <summary>true = đã hủy kịp (api chưa được gọi).</summary>
        public bool Cancel(string key)
        {
            if (_pending.TryRemove(key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                return true;
            }
            return false;
        }

        private async Task RunAsync(string key, CancellationTokenSource cts, Func<CancellationToken, Task> action)
        {
            try
            {
                await Task.Delay(Delay, time, cts.Token);
                if (!_pending.TryRemove(key, out _))
                {
                    return; // vừa bị hoàn tác
                }
                await action(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UndoWindow {Key}: việc sau hoàn tác lỗi", key);
            }
            finally
            {
                if (_pending.TryGetValue(key, out var current) && ReferenceEquals(current, cts))
                {
                    _pending.TryRemove(key, out _);
                }
            }
        }
    }
}
