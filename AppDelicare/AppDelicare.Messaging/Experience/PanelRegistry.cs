using System.Collections.Concurrent;
using AppDelicare.Messaging.Channels.Telegram.Models;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>BOT-UX-1: cấu hình bảng điều khiển (<c>Messaging:Panel</c>). Tắt → hành vi cũ (mỗi màn 1 tin mới).</summary>
    public sealed class PanelOptions
    {
        public const string Section = "Messaging:Panel";
        public bool Enabled { get; set; } = true;
        /// <summary>Telegram chỉ cho bot xóa tin trong 48 giờ – quá hạn thì sửa thành "Menu đã đóng" + bỏ nút.</summary>
        public int DeleteWithinHours { get; set; } = 48;
        public int RecentCount { get; set; } = 4;
    }

    /// <summary>
    /// 1 bước trong ngăn xếp điều hướng (breadcrumb): Root (màn chọn phân hệ) → Module → Action → Screen (BOT-UX-2: mỗi màn xem bên trong chức năng – thẻ, chọn giá trị).
    /// <paramref name="Replay"/> = callback CHỈ XEM để mở lại màn (Screen; Action khi đang ở trang / bộ lọc khác của danh sách – <c>pg:*</c>);
    /// <paramref name="Key"/> = định danh màn (callback bỏ phần giá trị – bấm −1 / +1 trên cùng màn không đẩy thêm); <paramref name="Detail"/> = thẻ chức năng tự mở
    /// (vd. chỉ có 1 chuyến → mở thẳng thẻ chuyến) – hiện trên đường dẫn, Quay lại bỏ qua.
    /// </summary>
    public sealed record PanelEntry(PanelLevel Level, string? Id, string Label, string? Replay = null, string? Key = null, string? Detail = null);

    public enum PanelLevel { Root, Module, Action, Screen }

    /// <summary>Trạng thái panel của 1 chat (khóa bot + chat). Chỉ ở bộ nhớ – khởi động lại thì panel cũ coi như tin thường.</summary>
    public sealed class PanelState
    {
        /// <summary>Tin của bot đang là panel (null = chưa có / đã đóng).</summary>
        public string? MessageId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<PanelEntry> Stack { get; } = [];
        /// <summary>
        /// BOT-UX-2: panel đang hiện ĐÚNG màn đầu ngăn xếp. false = đang hiện màn tạm (câu hỏi gõ tay, báo lỗi, phiếu kết quả) → Quay lại mở lại màn đầu ngăn xếp
        /// (không bỏ màn nào).
        /// </summary>
        public bool TopShown { get; set; } = true;
        /// <summary>BOT-UX-2 (mục 4): tin mang bàn phím dưới 3 phím đã gửi trong lần chạy này của container (gửi lại thì xóa tin cũ).</summary>
        public string? KeyboardMessageId { get; set; }
        public bool KeyboardShown { get; set; }
        /// <summary>Tin hỏi ForceReply đang chờ trả lời (nháp đang nhập) – Quay lại / Menu / Đóng thì xóa và hủy nháp.</summary>
        public List<string> DraftQuestions { get; } = [];
        /// <summary>Các panel cũ (đã thay) – bấm nút trên đó → "Menu này đã cũ".</summary>
        public LinkedList<string> OldPanels { get; } = new();
        /// <summary>⭐ Hay dùng: mã chức năng gần nhất (mới nhất đầu).</summary>
        public List<string> Recent { get; } = [];
        /// <summary>1 chat xử lý tuần tự (2 update cùng lúc không vẽ đè panel).</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public PanelEntry? Top => Stack.Count == 0 ? null : Stack[^1];

        public PanelEntry? CurrentAction => Stack.LastOrDefault(s => s.Level == PanelLevel.Action);

        public void Retire(string? messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return;
            }
            OldPanels.AddFirst(messageId);
            while (OldPanels.Count > 30)
            {
                OldPanels.RemoveLast();
            }
        }

        public void Touch(string actionCode, int max)
        {
            Recent.Remove(actionCode);
            Recent.Insert(0, actionCode);
            if (Recent.Count > max)
            {
                Recent.RemoveRange(max, Recent.Count - max);
            }
        }
    }

    /// <summary>BOT-UX-1 (mục 1): panel theo (bot, chat) – messageId, giờ tạo, ngăn xếp điều hướng, nháp đang nhập, ⭐ hay dùng.</summary>
    public sealed class PanelRegistry
    {
        private readonly ConcurrentDictionary<string, PanelState> _states = new();

        public PanelState Get(string bot, string chatId) => _states.GetOrAdd(bot + ":" + chatId, _ => new PanelState());

        public bool TryGet(string bot, string chatId, out PanelState? state) => _states.TryGetValue(bot + ":" + chatId, out state);
    }

    /// <summary>Cách đưa nội dung lên panel trong 1 lần xử lý: sửa panel hiện tại / xóa panel cũ + gửi panel mới ở cuối chat.</summary>
    public enum PanelMode { EditCurrent, NewAtBottom }

    /// <summary>
    /// BOT-UX-1: 1 lần xử lý update có panel (chat riêng, thao tác của người dùng). Gắn vào luồng async (AsyncLocal) – <see cref="PanelSender"/> đọc để biến
    /// "gửi tin" của chức năng thành "sửa panel". Tin hệ thống (OutboundPump, thẻ ca) chạy ngoài phiên nên không bao giờ bị panel đụng tới.
    /// </summary>
    public sealed class PanelSession(string bot, string chatId, PanelState state, PanelMode mode)
    {
        private static readonly AsyncLocal<PanelSession?> CurrentSession = new();

        public static PanelSession? Current => CurrentSession.Value is { Ended: false, Suspended: 0 } s ? s : null;

        public string Bot { get; } = bot;
        public string ChatId { get; } = chatId;
        public PanelState State { get; } = state;
        public PanelMode Mode { get; set; } = mode;
        public bool Ended { get; private set; }
        public int Suspended { get; private set; }
        /// <summary>Đã vẽ panel trong phiên này (lần sau chỉ sửa).</summary>
        public bool Rendered { get; set; }
        /// <summary>Nội dung chức năng gửi trong phiên (nhiều tin → ghép).</summary>
        public List<(string Html, List<List<TgInlineButton>> Rows)> Parts { get; } = [];
        /// <summary>BOT-UX-2: màn chức năng tự khai báo vừa vẽ (thẻ mẻ / chuyến / điểm…) – callback xem để mở lại + khóa màn. Xem <see cref="Screens.Show"/>.</summary>
        public (string Replay, string Key)? Screen { get; set; }
        /// <summary>BOT-UX-2: callback đang xử lý (nút vừa bấm, hoặc callback xem đang được phát lại khi ◀ Quay lại) – null = lệnh / chữ / chạy lại chức năng.</summary>
        public string? Callback { get; set; }
        /// <summary>BOT-UX-2: lần xử lý này mở chức năng (menu, ⭐, lệnh, Tiếp tục) – thẻ chức năng tự mở thì gộp vào bước chức năng.</summary>
        public bool EnteredAction { get; set; }

        public static PanelSession Begin(string bot, string chatId, PanelState state, PanelMode mode)
        {
            var s = new PanelSession(bot, chatId, state, mode);
            CurrentSession.Value = s;
            return s;
        }

        public void End()
        {
            Ended = true;
            if (CurrentSession.Value == this)
            {
                CurrentSession.Value = null;
            }
        }

        /// <summary>Tạm ra khỏi panel (vd. gửi + ghim Thẻ ca) – dùng với <c>using</c>.</summary>
        public static IDisposable Bypass()
        {
            var s = CurrentSession.Value;
            if (s == null)
            {
                return NoOp.Instance;
            }
            s.Suspended++;
            return new Resume(s);
        }

        private sealed class Resume(PanelSession s) : IDisposable
        {
            private bool _done;

            public void Dispose()
            {
                if (!_done)
                {
                    _done = true;
                    s.Suspended--;
                }
            }
        }

        private sealed class NoOp : IDisposable
        {
            public static readonly NoOp Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
