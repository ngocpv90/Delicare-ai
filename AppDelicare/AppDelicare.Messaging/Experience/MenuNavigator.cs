using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.4): 📂 Chức năng = lưới phân hệ người đó có quyền → bấm → SỬA CÙNG TIN thành danh sách chức năng + [⬅️ Quay lại].
    /// Tối đa 2 lần bấm tới bất kỳ chức năng nào (phân hệ chỉ có 1 → hiện thẳng chức năng). Callback <c>menu:root</c>, <c>menu:mod:&lt;mã&gt;</c>, <c>menu:act:&lt;mã&gt;</c>.
    /// BOT-UX-1: có panel (chat riêng) → màn đầu thêm hàng ⭐ Hay dùng (4 chức năng gần nhất), ghi ngăn xếp điều hướng; nút quay lại nằm ở hàng điều hướng của panel.
    /// </summary>
    public sealed class MenuNavigator(BotActionRegistry registry, IChannelSender sender, PanelController? panels = null)
    {
        public NeutralMessage Root(string bot, Core.Contracts.BotIdentityModel identity)
        {
            var actions = registry.Available(bot, identity);
            var modules = actions.Select(a => a.Module).Distinct().ToList();
            var message = modules.Count == 1 ? Module(bot, identity, modules[0], withBack: false) : new NeutralMessage
            {
                Title = "Chức năng",
                Lines = ["Chọn phân hệ:"],
                Buttons = [.. modules.Select(m => new NeutralButton(BotActionRegistry.ModuleLabel(m), CallbackData.Build(CallbackData.Kinds.Menu, "mod", m)))],
            };
            // BOT-UX-1 (mục 9): ⭐ Hay dùng – chức năng gần nhất của người này (bộ nhớ; khởi động lại thì mất).
            if (PanelSession.Current is { } s)
            {
                var recent = s.State.Recent.Select(c => actions.FirstOrDefault(a => a.Code == c)).Where(a => a != null).Select(a => a!).ToList();
                if (recent.Count > 0)
                {
                    message.Lines.Insert(0, "⭐ Hay dùng:");
                    message.Buttons.InsertRange(0, recent.Select(a => new NeutralButton("⭐ " + a.Label, CallbackData.Build(CallbackData.Kinds.Menu, "act", a.Code))));
                    if (recent.Count % 2 == 1)
                    {
                        // Lưới 2 cột: chừa ô để phân hệ bắt đầu ở hàng mới.
                        message.Buttons.Insert(recent.Count, new NeutralButton("·", CallbackData.Build(CallbackData.Kinds.Menu, "root")));
                    }
                }
            }
            return message;
        }

        public NeutralMessage Module(string bot, Core.Contracts.BotIdentityModel identity, string module, bool withBack = true)
        {
            var actions = registry.Available(bot, identity).Where(a => a.Module == module).ToList();
            var message = new NeutralMessage
            {
                Title = BotActionRegistry.ModuleLabel(module),
                Lines = actions.Count == 0 ? ["Không có chức năng nào trong quyền của bạn."] : [],
                Buttons = [.. actions.Select(a => new NeutralButton(a.Label, CallbackData.Build(CallbackData.Kinds.Menu, "act", a.Code)))],
            };
            if (withBack && PanelSession.Current == null)
            {
                message.Buttons.Add(new NeutralButton("⬅️ Quay lại", CallbackData.Build(CallbackData.Kinds.Menu, "root")));
            }
            return message;
        }

        public Task OpenAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (PanelSession.Current is { } s)
            {
                PanelController.EnterRoot(s.State);
            }
            return BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, Root(ctx.Bot, ctx.Identity), ct: ct);
        }

        /// <summary>BOT-UX-1: Quay lại từ chức năng → danh sách chức năng của phân hệ.</summary>
        public Task ShowModuleAsync(BotActionContext ctx, string module, CancellationToken ct)
        {
            if (PanelSession.Current is { } s)
            {
                PanelController.EnterModule(s.State, module);
            }
            return BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, Module(ctx.Bot, ctx.Identity, module), ct: ct);
        }

        /// <summary>Bấm nút trong menu: phân hệ / quay lại → sửa cùng tin; chức năng → chạy (kiểm lại quyền theo danh mục – nút cũ của người mất quyền không chạy).</summary>
        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (data.Action is "root" or "mod")
            {
                if (PanelSession.Current is { } s)
                {
                    if (data.Action == "root")
                    {
                        PanelController.EnterRoot(s.State);
                    }
                    else
                    {
                        PanelController.EnterModule(s.State, data.Id ?? string.Empty);
                    }
                }
                var m = data.Action == "root" ? Root(ctx.Bot, ctx.Identity) : Module(ctx.Bot, ctx.Identity, data.Id ?? string.Empty);
                var (html, kb) = TelegramFormatter.Render(m);
                if (ctx.Event.MessageId is { } mid)
                {
                    await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, mid.ToString(), html, kb, ct);
                }
                else
                {
                    await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, kb), ct);
                }
                return;
            }
            var action = registry.ByCode(data.Id);
            if (action == null || !action.IsAllowed(ctx.Bot, ctx.Identity))
            {
                await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, "Chức năng không còn trong quyền của bạn.", ct: ct);
                return;
            }
            if (PanelSession.Current is { } current)
            {
                panels?.EnterAction(current.State, ctx, action);
            }
            await action.Handler(ctx, ct);
        }
    }
}
