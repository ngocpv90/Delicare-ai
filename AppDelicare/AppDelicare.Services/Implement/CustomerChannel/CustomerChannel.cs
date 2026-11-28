using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Implement.Identity;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.CustomerChannel
{
    public sealed class CustomerLinkCodeRequest
    {
        public Guid ContactId { get; set; }
    }

    public sealed class CustomerLinkCodeModel
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
    }

    /// <summary>1 tin khách gửi tới OA (messaging đã kiểm chữ ký Zalo). EventId = msg_id của Zalo – chống xử lý 2 lần khi Zalo gửi lại.</summary>
    public sealed class CustomerChannelRequest
    {
        public string? BotCode { get; set; }
        public string? ExternalUserId { get; set; }
        public string? DisplayName { get; set; }
        public string? EventId { get; set; }
        public string? Text { get; set; }
    }

    public sealed class CustomerChannelReplyModel
    {
        public List<string> Replies { get; set; } = [];
        public bool Duplicate { get; set; }
    }

    public sealed class ZaloTokenModel
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// AI-19 (D-133 đợt 4, D-06 – anh chốt cột CustomerContact.UserId): bot Zalo cho KHÁCH HÀNG.
    /// <list type="bullet">
    /// <item>KD cấp <b>mã liên kết</b> cho 1 người liên hệ (Khách 360) → hệ thống tạo tài khoản CUSTOMER (D-82: không đăng nhập web, không gọi API nội bộ) gắn vào
    /// <c>CustomerContact.UserId</c>; khách gửi mã vào OA → <c>UserExternalIdentity</c> kênh ZALO.</item>
    /// <item>Mọi dữ liệu trả về CHỈ của khách đó (tra từ định danh Zalo → người liên hệ → khách; không nhận mã khách từ tin nhắn).</item>
    /// <item>Khách KHÔNG tự ghi: xác nhận suất / báo thiếu / phản ánh = đề xuất agent CSKH ở Phê duyệt › Lệnh của agent (KD / điều phối xác nhận); đổi món = yêu cầu đổi món
    /// như web (tự duyệt khi đủ 4 điều kiện D-91, còn lại chờ duyệt). Kết quả xác nhận được nhắn lại cho khách.</item>
    /// <item>Câu hỏi tự do → Hermes CSKH (nếu đã cấu hình) với người hỏi <c>zalo:&lt;id&gt;</c> – cổng công cụ chỉ cho công cụ dành cho khách.</item>
    /// </list>
    /// Mỗi tin vào ghi 1 dòng IntegrationLog (không lưu nội dung). Token Zalo OA lưu mã hóa (AES-GCM, khóa dẫn xuất từ Jwt:SigningKey) ở ConfigJson của kênh.
    /// </summary>
    public interface ICustomerChannelService
    {
        Task<IResult<CustomerLinkCodeModel>> CreateLinkCode(CustomerLinkCodeRequest request);
        Task<IResult<CustomerChannelReplyModel>> Handle(CustomerChannelRequest request);
        Task<IResult<ZaloTokenModel>> GetAccessToken();
    }

    public sealed partial class CustomerChannelService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration,
        IOptionsMonitor<ZaloOptions> zalo, IOptionsMonitor<MessagingOptions> messaging, IOptionsMonitor<AgentOptions> agentOptions, IServiceProvider services) : ICustomerChannelService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private static readonly SemaphoreSlim TokenLock = new(1, 1);
        private DatabaseContext Db => unitOfWork.DbContext;

        public const string Menu = "DELI-CARE – gõ số để chọn:\n1 · Đơn của tôi (7 ngày)\n2 · Thực đơn theo đơn\n3 <số đơn> · Xác nhận đơn (đổi số: 3 <số đơn> TRUA=120)\n"
            + "4 <số suất> [ghi chú] · Báo thiếu suất đơn hôm nay (4 <số đơn> <số suất> … cho đơn khác)\n5 <nội dung> · Gửi phản ánh\n6 <số đơn> · Đổi món\n0 · Hướng dẫn\n"
            + "Hoặc nhắn câu hỏi – Trợ lý trả lời theo đơn của quý khách.";

        // ---------------------------------------------------------------- mã liên kết (web – KD)
        public async Task<IResult<CustomerLinkCodeModel>> CreateLinkCode(CustomerLinkCodeRequest request)
        {
            var actor = currentActor.RequireActor();
            string? key = messaging.CurrentValue.LinkSigningKey;
            if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            {
                return Result<CustomerLinkCodeModel>.Fail(ConstantHelper.Message.BotLinkKeyMissing);
            }
            var contact = await Db.CustomerContact.FirstOrDefaultAsync(c => c.Id == request.ContactId && !c.IsDeleted);
            if (contact == null || (contact.Status != null && !string.Equals(contact.Status, "Active", StringComparison.OrdinalIgnoreCase)))
            {
                return Result<CustomerLinkCodeModel>.Fail("Không có người liên hệ này hoặc người liên hệ đã ngừng");
            }
            var customer = await Db.Customer.AsNoTracking().Where(c => c.Id == contact.CustomerId && !c.IsDeleted).Select(c => new { c.Code, c.Name, c.Status }).FirstOrDefaultAsync();
            if (customer == null)
            {
                return Result<CustomerLinkCodeModel>.Fail("Khách hàng không còn hoạt động");
            }
            DateTime now = clock.Now;
            if (contact.UserId == null || !await Db.User.AnyAsync(u => u.Id == contact.UserId && !u.IsDeleted))
            {
                // Tài khoản CUSTOMER (D-82): không có mật khẩu dùng được, không nhóm quyền – chỉ là danh tính của người liên hệ cho kênh khách.
                var user = new User
                {
                    Id = Guid.NewGuid(), UserName = "kh." + contact.Id.ToString("N")[..12], PassWord = "!NO-LOGIN!", IsActive = true, IsAdmin = false, AccountType = PersonLink.Customer,
                    CreatedDate = now, CreatedBy = actor.UserId, IsDeleted = false,
                };
                Db.User.Add(user);
                // EF không biết khóa ngoại CustomerContact.UserId (cột thêm bằng migration, không có điều hướng) → phải ghi User TRƯỚC rồi mới gắn (SQL Server FK_CustomerContact_User).
                await Db.SaveChangesAsync();
                contact.UserId = user.Id;
                contact.ModifiedDate = now;
                contact.ModifiedBy = actor.UserId;
            }
            Db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = contact.Id, EntityName = "CustomerContact", Action = "ZaloLinkCode",
                Changes = JsonSerializer.Serialize(new { customer = customer.Code, contact = contact.FullName ?? contact.Name, user = contact.UserId }), CreatedDate = now, CreatedBy = actor.UserId, IsDeleted = false,
            });
            await Db.SaveChangesAsync();
            DateTime exp = clock.UtcNow.AddHours(Math.Max(1, zalo.CurrentValue.LinkCodeHours));
            string code = LinkCodeSigner.Create(MessagingCode.LinkKind.CustomerContact, contact.Id, exp, key);
            string who = contact.FullName ?? contact.Name ?? "người liên hệ";
            return Result<CustomerLinkCodeModel>.Success(new CustomerLinkCodeModel
            {
                Code = code, ExpiresAt = now.AddHours(Math.Max(1, zalo.CurrentValue.LinkCodeHours)), ContactName = who,
                Instruction = $"Gửi cho {who} ({customer.Name}): quan tâm Zalo OA DELI-CARE rồi nhắn mã sau vào OA (dùng 1 lần, hạn {zalo.CurrentValue.LinkCodeHours} giờ): {code}",
            }, "OK");
        }

        // ---------------------------------------------------------------- tin khách (messaging, khóa client)
        public async Task<IResult<CustomerChannelReplyModel>> Handle(CustomerChannelRequest request)
        {
            string ext = (request.ExternalUserId ?? string.Empty).Trim();
            string bot = (request.BotCode ?? zalo.CurrentValue.BotCode).Trim();
            var bots = await RecipientResolver.ActiveBotsAsync(Db, default, MessagingCode.ProviderZalo);
            if (ext.Length == 0 || !bots.TryGetValue(bot, out Guid channel))
            {
                return Result<CustomerChannelReplyModel>.Fail("Bot Zalo khách chưa bật");
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) || system == Guid.Empty)
            {
                return Result<CustomerChannelReplyModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            DateTime now = clock.Now;
            string? eventKey = string.IsNullOrWhiteSpace(request.EventId) ? null : $"ZALO:{bot}:{request.EventId.Trim()}";
            if (eventKey != null && await Db.IntegrationLog.AsNoTracking().AnyAsync(l => l.IdempotencyKey == eventKey && !l.IsDeleted))
            {
                return Result<CustomerChannelReplyModel>.Success(new CustomerChannelReplyModel { Duplicate = true }, "Trùng");
            }
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                Db.IntegrationLog.Add(new IntegrationLog
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = channel, DirectionCode = MessagingCode.Log.In, OperationCode = MessagingCode.Log.OpReceive, IdempotencyKey = eventKey,
                    CorrelationId = Sha(ext), RequestAt = now, ResponseAt = now, LogStatus = MessagingCode.Log.StatusSuccess, CreatedDate = now, CreatedBy = system, IsDeleted = false,
                });
                await Db.SaveChangesAsync();
            }
            string text = (request.Text ?? string.Empty).Trim();
            var who = await WhoAsync(channel, ext);
            List<string> replies;
            if (who == null)
            {
                replies = LinkCodeRx().IsMatch(text) ? [await LinkAsync(channel, ext, request.DisplayName, text, system)]
                    : ["Chào quý khách! Để xem đơn và gửi yêu cầu qua Zalo, vui lòng nhắn MÃ LIÊN KẾT do nhân viên kinh doanh DELI-CARE gửi (dùng 1 lần)."];
            }
            else
            {
                replies = await DispatchAsync(who, ext, text);
            }
            int max = Math.Clamp(zalo.CurrentValue.MaxReplyLength, 200, 2000);
            return Result<CustomerChannelReplyModel>.Success(new CustomerChannelReplyModel { Replies = [.. replies.Where(r => r.Length > 0).Select(r => r.Length > max ? r[..(max - 1)] + "…" : r)] }, "OK");
        }

        private sealed record Who(Guid UserId, Guid CustomerId, string CustomerName, string ContactName);

        private async Task<Who?> WhoAsync(Guid channel, string ext)
            => await (from x in Db.UserExternalIdentity.AsNoTracking()
                      join u in Db.User.AsNoTracking() on x.UserId equals u.Id
                      join cc in Db.CustomerContact.AsNoTracking() on u.Id equals cc.UserId
                      join c in Db.Customer.AsNoTracking() on cc.CustomerId equals c.Id
                      where x.IntegrationChannelId == channel && x.ExternalUserId == ext && !x.IsDeleted && x.IdentityStatus == MessagingCode.Identity.Active
                            && !u.IsDeleted && u.IsActive && u.AccountType == PersonLink.Customer && !cc.IsDeleted && !c.IsDeleted
                      select new Who(u.Id, c.Id, c.DisplayName ?? c.Name ?? string.Empty, cc.FullName ?? cc.Name ?? string.Empty)).FirstOrDefaultAsync();

        private async Task<string> LinkAsync(Guid channel, string ext, string? displayName, string code, Guid system)
        {
            string? key = messaging.CurrentValue.LinkSigningKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                return ConstantHelper.Message.BotLinkKeyMissing;
            }
            string? reason = LinkCodeSigner.Verify(code, key, clock.UtcNow, out char kind, out Guid contactId, out _);
            if (reason != null || kind != MessagingCode.LinkKind.CustomerContact)
            {
                return reason ?? ConstantHelper.Message.BotLinkCodeInvalid;
            }
            string usedKey = LinkCodeSigner.UsedKey(code);
            if (await Db.IntegrationLog.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.IdempotencyKey == usedKey && l.LogStatus == MessagingCode.Log.StatusSuccess))
            {
                return ConstantHelper.Message.BotLinkCodeUsed;
            }
            var contact = await Db.CustomerContact.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contactId && !c.IsDeleted && c.UserId != null);
            if (contact == null)
            {
                return "Mã liên kết không còn hiệu lực – liên hệ nhân viên kinh doanh để lấy mã mới";
            }
            DateTime now = clock.Now;
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                var identity = await Db.UserExternalIdentity.FirstOrDefaultAsync(i => i.IntegrationChannelId == channel && i.ExternalUserId == ext);
                bool isNew = identity == null;
                identity ??= new UserExternalIdentity { Id = Guid.NewGuid(), IntegrationChannelId = channel, ExternalUserId = MessagingRules.Truncate(ext, 255)!, CreatedDate = now, CreatedBy = system };
                identity.UserId = contact.UserId;
                identity.EmployeeId = null;
                identity.DisplayName = MessagingRules.Truncate(displayName, 255) ?? identity.DisplayName;
                identity.LinkedAt = now;
                identity.LastSeenAt = now;
                identity.IdentityStatus = MessagingCode.Identity.Active;
                identity.IsDeleted = false;
                identity.ModifiedDate = now;
                identity.ModifiedBy = system;
                // Mỗi người liên hệ 1 Zalo: định danh cũ của cùng tài khoản trên kênh → Revoked.
                foreach (var other in await Db.UserExternalIdentity.Where(i => !i.IsDeleted && i.IntegrationChannelId == channel && i.Id != identity.Id && i.UserId == contact.UserId
                             && i.IdentityStatus == MessagingCode.Identity.Active).ToListAsync())
                {
                    other.IdentityStatus = MessagingCode.Identity.Revoked;
                }
                if (isNew)
                {
                    Db.UserExternalIdentity.Add(identity);
                }
                Db.IntegrationLog.Add(new IntegrationLog
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = channel, DirectionCode = MessagingCode.Log.Link, OperationCode = MessagingCode.Log.OpLinkCode, CorrelationId = identity.Id.ToString("N"),
                    IdempotencyKey = usedKey, RequestAt = now, ResponseAt = now, LogStatus = MessagingCode.Log.StatusSuccess, CreatedDate = now, CreatedBy = system, IsDeleted = false,
                });
                await Db.SaveChangesAsync();
            }
            var customer = await Db.Customer.AsNoTracking().Where(c => c.Id == contact.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync();
            return $"Đã liên kết Zalo với {contact.FullName ?? contact.Name} – {customer}.\n\n{Menu}";
        }

        // ---------------------------------------------------------------- lệnh khách
        private async Task<List<string>> DispatchAsync(Who who, string ext, string text)
        {
            string f = KnowledgeVault.Fold(text).Trim();
            var tok = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string head = tok.Length == 0 ? string.Empty : KnowledgeVault.Fold(tok[0]);
            try
            {
                if (f.Length == 0 || head is "0" or "menu" or "help" or "/start" || f is "huong dan" or "xin chao" or "chao")
                {
                    return [$"Chào {who.ContactName} ({who.CustomerName}).\n{Menu}"];
                }
                if (head == "1" || f.StartsWith("don cua toi", StringComparison.Ordinal) || f == "don")
                {
                    return [await OrdersTextAsync(who)];
                }
                if (head == "2" || f.StartsWith("thuc don", StringComparison.Ordinal))
                {
                    return [await MenuTextAsync(who)];
                }
                if (head == "3" || f.StartsWith("xac nhan", StringComparison.Ordinal))
                {
                    var rest = tok.Skip(f.StartsWith("xac nhan", StringComparison.Ordinal) ? 2 : 1).ToList();
                    if (rest.Count == 0)
                    {
                        return ["Ghi số đơn: 3 <số đơn> (vd 3 DH-261129-0012). Đổi số suất: 3 <số đơn> TRUA=120"];
                    }
                    var lines = rest.Skip(1).Select(t => t.Split('=')).Where(p => p.Length == 2 && decimal.TryParse(p[1], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                        .Select(p => new { loai_suat = p[0].ToUpperInvariant(), so_suat = decimal.Parse(p[1], CultureInfo.InvariantCulture) }).ToList();
                    object args = lines.Count > 0 ? new { so_don = rest[0], dong = lines, ly_do = $"Khách {who.ContactName} báo số suất qua Zalo" }
                        : new { so_don = rest[0], ly_do = $"Khách {who.ContactName} xác nhận đơn qua Zalo" };
                    return [await ProposeAsync(who, "propose_portion_confirm", args)];
                }
                if (head == "4" || f.StartsWith("thieu", StringComparison.Ordinal))
                {
                    var rest = tok.Skip(1).ToList();
                    string? order = null;
                    if (rest.Count > 0 && !decimal.TryParse(rest[0], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        order = rest[0];
                        rest = rest.Skip(1).ToList();
                    }
                    if (rest.Count == 0 || !decimal.TryParse(rest[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal miss))
                    {
                        return ["Ghi số suất thiếu: 4 <số suất> [ghi chú] (đơn hôm nay) hoặc 4 <số đơn> <số suất> [ghi chú]"];
                    }
                    string note = string.Join(' ', rest.Skip(1));
                    return [await ProposeAsync(who, "propose_shortage_report", new { so_don = order, so_suat_thieu = miss, ly_do = $"{who.ContactName}: {(note.Length > 0 ? note : "thiếu suất khi nhận")}" })];
                }
                if (head == "5" || f.StartsWith("phan anh", StringComparison.Ordinal))
                {
                    string content = string.Join(' ', tok.Skip(f.StartsWith("phan anh", StringComparison.Ordinal) ? 2 : 1)).Trim();
                    if (content.Length < 3)
                    {
                        return ["Ghi nội dung phản ánh: 5 <nội dung> (vd 5 Cơm trưa nay nguội, giao trễ 30 phút)"];
                    }
                    return [await ProposeAsync(who, "propose_complaint", new { loai = CategoryOf(KnowledgeVault.Fold(content)), noi_dung = content, ly_do = $"Phản ánh của {who.ContactName} qua Zalo" })];
                }
                if (head == "6" || f.StartsWith("doi mon", StringComparison.Ordinal))
                {
                    return [await DishChangeAsync(who, tok.Skip(f.StartsWith("doi mon", StringComparison.Ordinal) ? 2 : 1).ToList())];
                }
                return [await AskHermesAsync(who, ext, text) ?? $"Xin lỗi, DELI-CARE chưa hiểu yêu cầu.\n{Menu}"];
            }
            catch (AgentToolException ex)
            {
                return [ex.Message];
            }
        }

        private static string CategoryOf(string folded)
            => folded.Contains("tre") || folded.Contains("muon") ? "LATE"
            : folded.Contains("ve sinh") || folded.Contains("di vat") || folded.Contains("toc") || folded.Contains("dau bung") ? "HYGIENE"
            : folded.Contains("hop") || folded.Contains("khay") ? "PACKAGING"
            : folded.Contains("thai do") || folded.Contains("phuc vu") ? "SERVICE"
            : folded.Contains("nguoi") || folded.Contains("nhat") || folded.Contains("man") || folded.Contains("chat luong") || folded.Contains("song") || folded.Contains("thiu") ? "QUALITY"
            : "OTHER";

        private async Task<Guid> CskhUserAsync()
        {
            var id = await (from x in Db.UserExternalIdentity.AsNoTracking() join c in Db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals c.Id
                            where c.ChannelCode == AgentGatewayService.ChannelCode && x.ExternalUserId == "CSKH" && !x.IsDeleted && x.UserId != null select x.UserId).FirstOrDefaultAsync();
            return id ?? throw new AgentToolException("Agent CSKH chưa có tài khoản dịch vụ – liên hệ IT (Agent_seed)");
        }

        private async Task<string> ProposeAsync(Who who, string command, object args)
        {
            var def = AgentCommandRegistry.Find(command)!;
            Guid cskh = await CskhUserAsync();
            string correlation = "zalo:" + Guid.NewGuid().ToString("N")[..16];
            using var scope = currentActor.BeginScope(ActorInfo.ForAgent(cskh, "CSKH"), correlation);
            var ctx = new AgentToolContext { AgentCode = "CSKH", AgentUserId = cskh, AskingUserId = who.UserId, CustomerId = who.CustomerId, CorrelationId = correlation, Services = services };
            var p = await ((IAgentCommandService)services.GetService(typeof(IAgentCommandService))!).ProposeAsync(def, ctx, JsonSerializer.SerializeToElement(args));
            return $"Đã gửi yêu cầu {p.RequestNo}: {p.Summary}.\nNhân viên DELI-CARE sẽ xác nhận (trong giờ làm việc) và nhắn lại quý khách."
                + (p.Warnings.Count > 0 ? "\nLưu ý: " + string.Join("; ", p.Warnings) : string.Empty);
        }

        private async Task<string> OrdersTextAsync(Who who)
        {
            var list = await AgentToolRegistry.CustomerOrdersAsync(services, who.CustomerId, clock.Today, 7);
            if (list.Count == 0)
            {
                return "7 ngày tới quý khách chưa có đơn suất nào.";
            }
            var sb = new StringBuilder($"Đơn của {who.CustomerName} (7 ngày):");
            foreach (var o in list)
            {
                sb.Append($"\n• {o.MealDate:dd/MM} {o.OrderNumber} – {o.Status}: ").Append(string.Join(", ", o.Lines.Select(l => $"{l.MealType} {l.Quantity:0.#}")));
            }
            return sb.Append("\nXác nhận: 3 <số đơn> · Đổi món: 6 <số đơn>").ToString();
        }

        private async Task<string> MenuTextAsync(Who who)
        {
            var list = await AgentToolRegistry.CustomerMenuAsync(services, who.CustomerId, clock.Today, 7);
            if (list.Count == 0)
            {
                return "Chưa có thực đơn theo đơn của quý khách trong 7 ngày tới.";
            }
            var sb = new StringBuilder("Thực đơn theo đơn của quý khách:");
            foreach (var m in list)
            {
                sb.Append($"\n• {m.MealDate:dd/MM} {m.MealType}: ").Append(string.Join(", ", m.Dishes));
            }
            return sb.ToString();
        }

        /// <summary>D-91 e: 6 &lt;đơn&gt; → món hiện có; 6 &lt;đơn&gt; &lt;a&gt; → gợi ý thay; 6 &lt;đơn&gt; &lt;a&gt; &lt;b&gt; → tạo yêu cầu đổi món gắn đơn (tự duyệt khi đủ điều kiện).</summary>
        private async Task<string> DishChangeAsync(Who who, List<string> rest)
        {
            if (rest.Count == 0)
            {
                return "Ghi số đơn: 6 <số đơn>";
            }
            string no = rest[0];
            var order = await Db.Order.AsNoTracking().FirstOrDefaultAsync(o => !o.IsDeleted && o.CustomerId == who.CustomerId && (o.OrderNumber == no || o.OrderNo == no))
                ?? throw new AgentToolException($"Không tìm thấy đơn «{no}» của quý khách");
            var svc = (IMenuChangeRequestService)services.GetService(typeof(IMenuChangeRequestService))!;
            Guid cskh = await CskhUserAsync();
            using var scope = currentActor.BeginScope(ActorInfo.ForAgent(cskh, "CSKH"), "zalo:" + Guid.NewGuid().ToString("N")[..16]);
            var pre = await svc.GetOrderDishChange(new OrderDishChangeRequest { OrderId = order.Id, Source = "ZALO" });
            var dishes = pre.Data?.ListDish ?? [];
            if (pre.ResponseCode != 200 || dishes.Count == 0)
            {
                return pre.ResponseMessage is { Length: > 0 } m && pre.ResponseCode != 200 ? m : $"Đơn {order.OrderNumber} chưa có món để đổi.";
            }
            if (rest.Count == 1 || !int.TryParse(rest[1], out int a) || a < 1 || a > dishes.Count)
            {
                return $"Món của đơn {order.OrderNumber}:\n" + string.Join("\n", dishes.Select((d, i) => $"{i + 1}. {d.DishName} ({d.MealName})")) + $"\nChọn món muốn đổi: 6 {order.OrderNumber} <số món>";
            }
            var src = dishes[a - 1];
            var sug = await svc.GetOrderDishChange(new OrderDishChangeRequest { OrderId = order.Id, OriginalDishId = src.DishId, MealType = src.MealType, Source = "ZALO" });
            var options = (sug.Data?.ListSuggestion ?? []).Where(s => s.HasRecipe).Take(8).ToList();
            if (rest.Count == 2 || !int.TryParse(rest[2], out int b) || b < 1 || b > options.Count)
            {
                return options.Count == 0 ? $"Chưa có món thay phù hợp cho {src.DishName} – liên hệ nhân viên kinh doanh."
                    : $"Món thay cho {src.DishName}:\n" + string.Join("\n", options.Select((s, i) => $"{i + 1}. {s.DishName}")) + $"\nChọn: 6 {order.OrderNumber} {a} <số món thay>";
            }
            var r = await svc.CreateForOrder(new OrderDishChangeRequest
            {
                OrderId = order.Id, OriginalDishId = src.DishId, MealType = src.MealType, RequestedDishId = options[b - 1].DishId, Source = "ZALO", Note = $"Khách {who.ContactName} đổi món qua Zalo",
            });
            if (r.ResponseCode != 200 || r.Data == null)
            {
                return r.ResponseMessage ?? "Không tạo được yêu cầu đổi món";
            }
            return r.Data.AutoApproved > 0 && r.Data.SentToApprove == 0 ? $"Đã đổi {src.DishName} → {options[b - 1].DishName} cho đơn {order.OrderNumber}."
                : r.Data.SentToApprove > 0 ? $"Đã gửi yêu cầu đổi {src.DishName} → {options[b - 1].DishName} ({string.Join(", ", r.Data.ListRequestNo)}) – chờ DELI-CARE duyệt, sẽ nhắn lại quý khách."
                : string.Join("; ", r.Data.ListError.DefaultIfEmpty("Không đổi được món"));
        }

        /// <summary>Câu hỏi tự do → Hermes CSKH (API server kiểu OpenAI); người hỏi zalo:&lt;id&gt; ở header – cổng công cụ chỉ cho công cụ dành cho khách. Chưa cấu hình → null.</summary>
        private async Task<string?> AskHermesAsync(Who who, string ext, string text)
        {
            var o = agentOptions.CurrentValue;
            string? url = AgentChatService.ChatUrl(o, "CSKH");
            if (!o.Enabled || string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Post, url.TrimEnd('/') + "/chat/completions")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { model = "delicare-cskh", messages = new[] { new { role = "user", content = text.Length > 2000 ? text[..2000] : text } }, user = AgentGatewayService.ZaloAskerPrefix + ext, stream = false }),
                        Encoding.UTF8, "application/json"),
                };
                msg.Headers.Add("X-On-Behalf-Of", AgentGatewayService.ZaloAskerPrefix + ext);
                msg.Headers.Add("X-Agent-Code", "CSKH");
                if (Environment.GetEnvironmentVariable(o.HermesApiKeyEnv) is { Length: > 0 } key)
                {
                    msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                }
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(o.ChatTimeoutSeconds, 5, 120)));
                using var res = await Http.SendAsync(msg, cts.Token);
                if (!res.IsSuccessStatusCode)
                {
                    return null;
                }
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token));
                string reply = AgentChatService.ReadReply(doc.RootElement);
                return reply.Length == 0 ? null : reply;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
            {
                Log.Warning(ex, "[CustomerChannel] Hermes CSKH không phản hồi");
                return null;
            }
        }

        // ---------------------------------------------------------------- token Zalo OA
        private byte[] TokenKey => SHA256.HashData(Encoding.UTF8.GetBytes("zalo-token:" + (configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Thiếu Jwt:SigningKey"))));

        public static string Protect(byte[] key, string plain)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12), data = Encoding.UTF8.GetBytes(plain), cipher = new byte[data.Length], tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, data, cipher, tag);
            return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        public static string? Unprotect(byte[] key, string? value)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                byte[] all = Convert.FromBase64String(value), plain = new byte[all.Length - 28];
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
            {
                return null;
            }
        }

        public async Task<IResult<ZaloTokenModel>> GetAccessToken()
        {
            var z = zalo.CurrentValue;
            var ch = await Db.IntegrationChannel.FirstOrDefaultAsync(c => !c.IsDeleted && c.ProviderCode == MessagingCode.ProviderZalo);
            if (ch == null || ch.ChannelStatus != MessagingCode.StatusActive)
            {
                return Result<ZaloTokenModel>.Fail("Kênh Zalo OA chưa bật");
            }
            await TokenLock.WaitAsync();
            try
            {
                var cfg = JsonNode.Parse(string.IsNullOrWhiteSpace(ch.ConfigJson) ? "{}" : ch.ConfigJson) as JsonObject ?? [];
                byte[] k = TokenKey;
                string? at = Unprotect(k, cfg["at"]?.GetValue<string>());
                DateTime atExp = DateTime.TryParse(cfg["atExp"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e) ? e : DateTime.MinValue;
                if (at != null && atExp > DateTime.UtcNow.AddMinutes(5))
                {
                    return Result<ZaloTokenModel>.Success(new ZaloTokenModel { AccessToken = at, ExpiresAt = atExp }, "OK");
                }
                string? rt = Unprotect(k, cfg["rt"]?.GetValue<string>()) ?? z.RefreshToken;
                if (string.IsNullOrWhiteSpace(z.AppId) || string.IsNullOrWhiteSpace(z.AppSecret) || string.IsNullOrWhiteSpace(rt))
                {
                    return Result<ZaloTokenModel>.Fail("Thiếu Zalo:AppId / Zalo__AppSecret / Zalo__RefreshToken – IT cấu hình trên server");
                }
                using var msg = new HttpRequestMessage(HttpMethod.Post, z.TokenUrl)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["refresh_token"] = rt, ["app_id"] = z.AppId!, ["grant_type"] = "refresh_token" }),
                };
                msg.Headers.Add("secret_key", z.AppSecret);
                using var res = await Http.SendAsync(msg);
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                if (!root.TryGetProperty("access_token", out var atEl) || !root.TryGetProperty("refresh_token", out var rtEl))
                {
                    string err = root.TryGetProperty("error_description", out var d) ? d.GetString() ?? "" : root.GetRawText();
                    Log.Warning("[Zalo] làm mới token lỗi: {Err}", err);
                    return Result<ZaloTokenModel>.Fail("Zalo từ chối làm mới token – IT cấp lại quyền OA (Zalo__RefreshToken mới)");
                }
                int sec = root.TryGetProperty("expires_in", out var ex) ? ex.ValueKind == JsonValueKind.Number ? ex.GetInt32() : int.TryParse(ex.GetString(), out int s) ? s : 3600 : 3600;
                DateTime exp = DateTime.UtcNow.AddSeconds(sec);
                cfg["at"] = Protect(k, atEl.GetString()!);
                cfg["atExp"] = exp.ToString("o", CultureInfo.InvariantCulture);
                cfg["rt"] = Protect(k, rtEl.GetString()!); // Zalo đổi token làm mới mỗi lần – phải lưu bản mới
                ch.ConfigJson = cfg.ToJsonString();
                ch.ModifiedDate = clock.Now;
                await Db.SaveChangesAsync();
                return Result<ZaloTokenModel>.Success(new ZaloTokenModel { AccessToken = atEl.GetString()!, ExpiresAt = exp }, "OK");
            }
            finally
            {
                TokenLock.Release();
            }
        }

        private static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..32].ToLowerInvariant();

        [GeneratedRegex("^[A-Za-z0-9_-]{40,60}$")]
        private static partial Regex LinkCodeRx();
    }

    /// <summary>AI-19: nhắn kết quả cho khách qua Zalo (tin chờ gửi kênh ZALO – messaging ZaloOutboundPump gửi). Khách chưa liên kết / kênh tắt → bỏ qua.</summary>
    public static class CustomerNotifier
    {
        public const string Template = "CUSTOMER_NOTICE";

        public static async Task<bool> EnqueueAsync(DatabaseContext db, Guid userId, string text, string key, DateTime now, Guid? by)
        {
            var target = await (from x in db.UserExternalIdentity.AsNoTracking()
                                join c in db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals c.Id
                                where x.UserId == userId && !x.IsDeleted && x.IdentityStatus == MessagingCode.Identity.Active && c.ProviderCode == MessagingCode.ProviderZalo && !c.IsDeleted
                                select new { c.Id, x.ExternalUserId }).FirstOrDefaultAsync();
            if (target == null || await db.OutboundMessage.AsNoTracking().AnyAsync(m => m.IdempotencyKey == key && !m.IsDeleted))
            {
                return false;
            }
            db.OutboundMessage.Add(new OutboundMessage
            {
                Id = Guid.NewGuid(), IntegrationChannelId = target.Id, ExternalConversationId = target.ExternalUserId, RecipientExternalId = target.ExternalUserId, MessageType = Template,
                PayloadJson = JsonSerializer.Serialize(new { template = Template, data = new { text } }), QueuedAt = now, MessageStatus = MessagingCode.Outbound.Queued, RetryCount = 0,
                IdempotencyKey = key, CreatedDate = now, CreatedBy = by, IsDeleted = false,
            });
            return true;
        }
    }
}
