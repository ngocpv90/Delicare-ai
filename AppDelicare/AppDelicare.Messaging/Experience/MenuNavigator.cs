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
    /// </summary>
    public sealed class MenuNavigator(BotActionRegistry registry, IChannelSender sender)
    {
        public NeutralMessage Root(string bot, Core.Contracts.BotIdentityModel identity)
        {
            var actions = registry.Available(bot, identity);
            var modules = actions.Select(a => a.Module).Distinct().ToList();
            if (modules.Count == 1)
            {
                return Module(bot, identity, modules[0], withBack: false);
            }
            return new NeutralMessage
            {
                Title = "Chức năng",
                Lines = ["Chọn phân hệ:"],
                Buttons = [.. modules.Select(m => new NeutralButton(BotActionRegistry.ModuleLabel(m), CallbackData.Build(CallbackData.Kinds.Menu, "mod", m)))],
            };
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
            if (withBack)
            {
                message.Buttons.Add(new NeutralButton("⬅️ Quay lại", CallbackData.Build(CallbackData.Kinds.Menu, "root")));
            }
            return message;
        }

        public Task OpenAsync(BotActionContext ctx, CancellationToken ct)
            => BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, Root(ctx.Bot, ctx.Identity), ct: ct);

        /// <summary>Bấm nút trong menu: phân hệ / quay lại → sửa cùng tin; chức năng → chạy (kiểm lại quyền theo danh mục – nút cũ của người mất quyền không chạy).</summary>
        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (data.Action is "root" or "mod")
            {
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
            await action.Handler(ctx, ct);
        }
    }
}
