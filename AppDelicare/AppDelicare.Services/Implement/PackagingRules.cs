using System.Text.RegularExpressions;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.5 (T20, spec 10.3 pod / 10.4, Catalog 190, 194–196) – sổ bao bì <c>PackagingLedger</c> (chỉ ghi thêm): OUT khi bàn giao, IN khi thu hồi,
    /// LOSS khi ghi nhận thất thoát, AVAILABLE khi mẻ vệ sinh đạt. Số còn ở khách = Σ OUT − Σ IN − Σ LOSS theo (khách, loại bao bì);
    /// theo đơn giao: cùng <c>ReferenceNo</c> = số đơn giao. PROPOSED – BA/PO CONFIRM (D-54 a, b; GAP-PKG-01…03).
    /// </summary>
    public static partial class PackagingRules
    {
        /// <summary>ContainerReturn không có cột loại bao bì (GAP-PKG-02): lưu tag <c>[BAO_BI:&lt;loại&gt;]</c> đầu <c>Note</c>.</summary>
        private const string TypeTag = "BAO_BI";

        [GeneratedRegex(@"^\[BAO_BI:([^\]]+)\]\s*")]
        private static partial Regex TypeTagRegex();

        public static string? TypeOf(string? note)
        {
            var m = TypeTagRegex().Match(note ?? string.Empty);
            return m.Success ? m.Groups[1].Value.Replace('⟧', ']').Trim() : null;
        }

        public static string? NoteText(string? note) => DeliveryRules.Clean(TypeTagRegex().Replace(note ?? string.Empty, string.Empty));

        public static string WithType(string? note, string type)
        {
            string text = NoteText(note) ?? string.Empty;
            // Loại bao bì có dấu "]" (vd. "[DEMO] KHAY") → ghi "⟧" trong tag để đọc lại đúng.
            string tagged = $"[{TypeTag}:{type.Replace(']', '⟧')}] {text}".Trim();
            return tagged;
        }

        public static string Num(decimal value) => ProductionRules.Num(value);

        /// <summary>Loại bao bì của đơn hàng: loại ghi ở kết quả đóng gói mới nhất của lệnh đóng gói thuộc đơn, không có thì <c>PackagingDefaultType</c>.</summary>
        public static async Task<string> DefaultTypeAsync(DatabaseContext db, Guid orderId, BusinessRulesOptions rules)
        {
            var packingIds = await db.PackingOrder.AsNoTracking().Where(p => !p.IsDeleted && p.OrderId == orderId).Select(p => p.Id).ToListAsync();
            string? type = packingIds.Count == 0 ? null : await db.PackingResult.AsNoTracking()
                .Where(r => !r.IsDeleted && packingIds.Contains(r.PackingOrderId) && r.PackagingType != null && r.PackagingType != "")
                .OrderByDescending(r => r.CreatedDate).Select(r => r.PackagingType).FirstOrDefaultAsync();
            return DeliveryRules.Clean(type) ?? rules.PackagingDefaultType;
        }

        /// <summary>Thêm 1 dòng sổ bao bì gắn đơn giao (khách, điểm giao, đơn hàng, chuyến, số đơn giao).</summary>
        public static PackagingLedger Add(IUnitOfWork uow, ActorInfo actor, DateTime at, string transactionType, string packagingType, decimal quantity,
            DeliveryOrder? deliveryOrder, Guid? customerId, string? condition, string? note, string? referenceNo = null)
        {
            var row = new PackagingLedger
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId ?? deliveryOrder?.CustomerId,
                DeliveryLocationId = deliveryOrder?.CustomerDeliveryLocationId ?? deliveryOrder?.DeliveryLocationId,
                OrderId = deliveryOrder?.OrderId,
                DeliveryRouteId = deliveryOrder?.DeliveryRouteId,
                TransactionType = transactionType,
                PackagingType = packagingType.Length > 100 ? packagingType[..100] : packagingType,
                Quantity = quantity,
                TransactionAt = at,
                ReferenceNo = referenceNo ?? deliveryOrder?.DeliveryNumber ?? deliveryOrder?.DeliveryNo,
                ConditionCode = condition,
                RecordedBy = actor.UserId,
                Note = note is { Length: > 1000 } ? note[..1000] : note,
            };
            uow.PackagingLedgerRepository.Add(row);
            return row;
        }

        /// <summary>Số còn ở khách (OUT − IN − LOSS) – theo khách + loại, tùy chọn theo số đơn giao; cộng cả dòng đang thêm (chưa lưu).</summary>
        public static async Task<decimal> OutstandingAsync(DatabaseContext db, Guid customerId, string packagingType, string? referenceNo = null)
        {
            var query = db.PackagingLedger.AsNoTracking().Where(l => !l.IsDeleted && l.CustomerId == customerId && l.PackagingType == packagingType);
            if (referenceNo != null)
            {
                query = query.Where(l => l.ReferenceNo == referenceNo);
            }
            var rows = await query.Select(l => new { l.TransactionType, l.Quantity }).ToListAsync();
            var pending = db.ChangeTracker.Entries<PackagingLedger>().Where(e => e.State == EntityState.Added).Select(e => e.Entity)
                .Where(l => l.CustomerId == customerId && l.PackagingType == packagingType && (referenceNo == null || l.ReferenceNo == referenceNo))
                .Select(l => new { l.TransactionType, l.Quantity });
            return rows.Concat(pending).Sum(r => Sign(r.TransactionType) * r.Quantity);
        }

        /// <summary>Loại bao bì đã giao (OUT) theo số đơn giao.</summary>
        public static Task<List<string>> DeliveredTypesAsync(DatabaseContext db, Guid customerId, string referenceNo)
            => db.PackagingLedger.AsNoTracking()
                .Where(l => !l.IsDeleted && l.CustomerId == customerId && l.ReferenceNo == referenceNo && l.TransactionType == PackagingTransactionType.Out)
                .Select(l => l.PackagingType!).Distinct().ToListAsync();

        /// <summary>+1 giao cho khách, −1 khách trả / thất thoát, 0 dòng nội bộ (AVAILABLE).</summary>
        public static decimal Sign(string? type) => type switch
        {
            PackagingTransactionType.Out => 1m,
            PackagingTransactionType.In or PackagingTransactionType.Loss => -1m,
            _ => 0m,
        };

        /// <summary>Ngưỡng thất thoát của khách: <c>Customer.ExternalData</c> JSON <c>{"containerLossMaxPct": n}</c> (GAP-PKG-01), không có → <c>KpiContainerLossMaxPct</c>.</summary>
        public static decimal ThresholdOf(Customer? customer, BusinessRulesOptions rules)
        {
            if (!string.IsNullOrWhiteSpace(customer?.ExternalData) && customer.ExternalData.TrimStart().StartsWith('{'))
            {
                try
                {
                    var token = JObject.Parse(customer.ExternalData)["containerLossMaxPct"];
                    if (token != null && token.Type is JTokenType.Integer or JTokenType.Float)
                    {
                        return token.Value<decimal>();
                    }
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    // ExternalData không phải JSON hợp lệ → dùng ngưỡng chung.
                }
            }
            return rules.KpiContainerLossMaxPct;
        }

        /// <summary>
        /// Số dư theo (khách, loại): lũy kế tới <paramref name="to"/> (OUT / IN / hỏng / LOSS / còn ở khách) + tỷ lệ thất thoát trong kỳ (Σ LOSS ÷ Σ OUT trong [from, to]).
        /// </summary>
        public static async Task<List<PackagingBalanceItemModel>> BalanceAsync(DatabaseContext db, BusinessRulesOptions rules, Guid? customerId, string? packagingType,
            DateTime? from, DateTime? to)
        {
            var query = db.PackagingLedger.AsNoTracking().Where(l => !l.IsDeleted && l.CustomerId != null
                && (l.TransactionType == PackagingTransactionType.Out || l.TransactionType == PackagingTransactionType.In || l.TransactionType == PackagingTransactionType.Loss));
            if (customerId is { } cid && cid != Guid.Empty)
            {
                query = query.Where(l => l.CustomerId == cid);
            }
            if (!string.IsNullOrWhiteSpace(packagingType))
            {
                string type = packagingType.Trim();
                query = query.Where(l => l.PackagingType == type);
            }
            if (to is { } t)
            {
                DateTime end = t.Date.AddDays(1);
                query = query.Where(l => l.TransactionAt < end);
            }
            var rows = await query.Select(l => new { CustomerId = l.CustomerId!.Value, l.PackagingType, l.TransactionType, l.Quantity, l.ConditionCode, l.TransactionAt }).ToListAsync();
            var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
            var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
            DateTime? start = from?.Date;
            return [.. rows.GroupBy(r => (r.CustomerId, Type: r.PackagingType ?? string.Empty)).Select(g =>
            {
                customers.TryGetValue(g.Key.CustomerId, out var customer);
                decimal Sum(string type) => g.Where(r => r.TransactionType == type).Sum(r => r.Quantity);
                decimal PeriodSum(string type) => g.Where(r => r.TransactionType == type && (start == null || r.TransactionAt >= start)).Sum(r => r.Quantity);
                decimal outQty = Sum(PackagingTransactionType.Out), inQty = Sum(PackagingTransactionType.In), loss = Sum(PackagingTransactionType.Loss);
                decimal periodOut = PeriodSum(PackagingTransactionType.Out), periodLoss = PeriodSum(PackagingTransactionType.Loss);
                decimal? rate = periodOut > 0 ? Math.Round(periodLoss / periodOut * 100m, 2, MidpointRounding.AwayFromZero) : null;
                decimal threshold = ThresholdOf(customer, rules);
                return new PackagingBalanceItemModel
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerCode = customer?.Code,
                    CustomerName = customer?.DisplayName ?? customer?.Name,
                    PackagingType = g.Key.Type,
                    OutQuantity = outQty,
                    InQuantity = inQty,
                    DamagedInQuantity = g.Where(r => r.TransactionType == PackagingTransactionType.In && r.ConditionCode == PackagingCondition.Damaged).Sum(r => r.Quantity),
                    LossQuantity = loss,
                    Outstanding = outQty - inQty - loss,
                    PeriodOutQuantity = periodOut,
                    PeriodLossQuantity = periodLoss,
                    LossRatePct = rate,
                    LossThresholdPct = threshold,
                    IsOverThreshold = rate != null && rate > threshold,
                };
            }).OrderBy(x => x.CustomerName, StringComparer.CurrentCulture).ThenBy(x => x.PackagingType, StringComparer.Ordinal)];
        }

        /// <summary>Cảnh báo vượt ngưỡng thất thoát của (khách, loại) sau khi ghi (BUSINESS_RULES §8: báo khách rồi chuyển kế toán tính phí).</summary>
        public static async Task<PackagingBalanceItemModel?> CheckThresholdAsync(DatabaseContext db, BusinessRulesOptions rules, Guid customerId, string packagingType, List<string> warnings)
        {
            var balance = (await BalanceAsync(db, rules, customerId, packagingType, null, null)).FirstOrDefault();
            if (balance is { IsOverThreshold: true })
            {
                warnings.Add(string.Format(ConstantHelper.Message.PackagingLossOverThreshold, balance.CustomerName, balance.PackagingType,
                    Num(balance.LossRatePct!.Value), Num(balance.LossThresholdPct)));
            }
            return balance;
        }
    }
}
