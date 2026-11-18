using AppDelicare.Messaging.Experience;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-1.4: chức năng phần chung (VH và DH – BOT_TELEGRAM §3.1). ⏳ Chờ tôi duyệt chỉ hiện khi có ít nhất 1 mục Duyệt được cấp rõ (PendingCode → không hiện).
    /// Phân hệ nghiệp vụ (Bếp, Kho, Giao vận…) thêm ở BOT-1.5 / 1.6 bằng provider riêng.
    /// </summary>
    public sealed class ChungActionProvider(HelpActions help, SelfServiceActions self, ApprovalActions approvals) : IBotActionProvider
    {
        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "my_shift", Module = "CHUNG", Label = KeyboardBuilder.MyShift, Order = 1, Handler = (c, ct) => help.MyShiftAsync(c.Bot, c.Event, c.Identity, ct) },
            new BotAction { Code = "check_in", Module = "CHUNG", Label = KeyboardBuilder.CheckIn, Command = "chamcong", CommandDescription = "Chấm công vào / ra", Order = 2, Handler = self.PromptLocationAsync },
            new BotAction { Code = "my_requests", Module = "CHUNG", Label = "📝 Đơn của tôi", Command = "donct", CommandDescription = "Đơn nghỉ / đổi ca của tôi", Order = 3, Handler = self.MyRequestsAsync },
            new BotAction
            {
                Code = "approvals", Module = "DUYET", Label = "⏳ Chờ tôi duyệt", Command = "cho", CommandDescription = "Việc chờ tôi duyệt", Order = 1,
                Visible = i => i.HasWebAccount && i.ApproveScreens.Count > 0, Handler = approvals.ListAsync,
            },
            new BotAction { Code = "help", Module = "CHUNG", Label = KeyboardBuilder.Help, Command = "help", CommandDescription = "Trợ giúp", Order = 9, Handler = (c, ct) => help.HelpAsync(c.Bot, c.Event, ct) },
        ];
    }
}
