using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-5 (D-131 i – anh chốt 2026-11-26): Kho › Trả hàng NCC (#/scm-supplier-return). Không bảng mới (D-126 tự chọn): phiếu trả = 1 dòng
    /// <c>SupplierIssue</c> (IssueType <c>RETURN</c>), dữ liệu phiếu ở thẻ <c>[TRA_NCC:{json}]</c> cuối Description, lịch sử ở AuditChange
    /// (EntityName <c>SupplierReturn</c>). Trạng thái hiển thị lấy từ IssueStatus chuẩn: Open = chờ NCC xác nhận, InProgress = NCC đồng ý,
    /// Resolved = đã ghi sổ, Closed = NCC không nhận / đã hủy (phân biệt bằng "st").
    /// <list type="number">
    /// <item>Thủ kho lập (quyền Thêm): chọn lô còn tồn, số trả ≤ tồn của lô, lý do (ReferenceLists:SupplierReturnReason) → lô "Tạm giữ" (OnHold),
    /// bot Vận hành báo Mua hàng (OPS-SCM / OPS-SCM-HEAD).</item>
    /// <item>Mua hàng xác nhận (quyền Sửa): NCC đồng ý → chờ giao trả; NCC không nhận (ghi chú bắt buộc) → lô bỏ tạm giữ.</item>
    /// <item>Thủ kho ghi sổ khi NCC nhận hàng (quyền Sửa): sổ kho <c>RETURN</c> (xuất, ReferenceType <c>SupplierReturn</c>, giá = giá lô) – kiểm lại tồn,
    /// chặn kỳ kế toán đã khóa (ACC-4); lô bỏ tạm giữ (hết tồn → Consumed). Chứng từ kế toán "Trả lại hàng mua" (ACC-2, đối tượng = NCC).</item>
    /// <item>Hủy (chưa ghi sổ) – lô bỏ tạm giữ.</item>
    /// </list>
    /// </summary>
    public class SupplierReturnService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IInventoryLedgerService ledger,
        IOptions<ReferenceListsOptions> referenceLists, INotificationService notifications) : ISupplierReturnService
    {
        public const string IssueType = "RETURN";
        public const string Entity = "SupplierReturn";
        public const string ReferenceType = "SupplierReturn";
        public const string ScreenId = "scm-supplier-return";
        private const string Tag = "[TRA_NCC:";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IInventoryLedgerService _ledger = ledger;
        private readonly ReferenceListsOptions _lists = referenceLists.Value;
        private readonly INotificationService _notifications = notifications;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Dữ liệu phiếu trong thẻ [TRA_NCC:…].</summary>
        public sealed class Payload
        {
            [JsonProperty("lot")] public Guid Lot { get; set; }
            [JsonProperty("wh")] public Guid Warehouse { get; set; }
            [JsonProperty("loc")] public Guid? Location { get; set; }
            [JsonProperty("ing")] public Guid Ingredient { get; set; }
            [JsonProperty("unit")] public Guid Unit { get; set; }
            [JsonProperty("qty")] public decimal Quantity { get; set; }
            [JsonProperty("cost")] public decimal? UnitCost { get; set; }
            [JsonProperty("rs")] public string? Reason { get; set; }
            [JsonProperty("note")] public string? Note { get; set; }
            [JsonProperty("link")] public Guid? LinkedIssue { get; set; }
            [JsonProperty("st")] public string? State { get; set; }
            [JsonProperty("prev")] public string? PrevLotStatus { get; set; }
            [JsonProperty("txn")] public Guid? Transaction { get; set; }
            [JsonProperty("pd")] public DateTime? PostedDate { get; set; }
        }

        public static string NumberOf(SupplierIssue i) => $"TH-{i.ReportedAt:yyMMdd}-{i.Id.ToString("N")[..4].ToUpperInvariant()}";

        public static Payload? Parse(string? description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return null;
            }
            int at = description.LastIndexOf(Tag, StringComparison.Ordinal);
            if (at < 0 || !description.EndsWith(']'))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<Payload>(description[(at + Tag.Length)..^1]);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Write(string text, Payload p)
        {
            string json = JsonConvert.SerializeObject(p, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string head = text.Length + json.Length + Tag.Length + 2 > 2000 ? text[..Math.Max(0, 2000 - json.Length - Tag.Length - 2)] : text;
            return $"{head} {Tag}{json}]";
        }

        private static string HeadOf(string? description)
        {
            int at = (description ?? string.Empty).LastIndexOf(Tag, StringComparison.Ordinal);
            return (at < 0 ? description ?? string.Empty : description![..at]).TrimEnd();
        }

        public static string StateOf(SupplierIssue i, Payload? p) => i.IssueStatus switch
        {
            SupplierIssueStatus.Open => SupplierReturnState.Pending,
            SupplierIssueStatus.InProgress => SupplierReturnState.Agreed,
            SupplierIssueStatus.Resolved => SupplierReturnState.Posted,
            _ => p?.State == SupplierReturnState.Refused ? SupplierReturnState.Refused : SupplierReturnState.Cancelled,
        };

        private List<ReferenceItem> Reasons() => _lists.Lists.TryGetValue("SupplierReturnReason", out var l) && l.Count > 0 ? l :
        [
            new() { Code = "DAMAGED", Label = "Hàng hỏng / biến chất" }, new() { Code = "SPEC", Label = "Sai quy cách" },
            new() { Code = "EXPIRY", Label = "Gần / quá hạn dùng" }, new() { Code = "EXCESS", Label = "Giao thừa" },
        ];

        private static AuditChange Audit(Guid id, string action, string? note, Guid actor, DateTime now)
            => new() { Id = Guid.NewGuid(), IdChange = id, EntityName = Entity, Action = action, Changes = JsonConvert.SerializeObject(new { note }), CreatedDate = now, CreatedBy = actor, IsDeleted = false };

        private static string ActionName(string a) => a switch
        {
            "Created" => "Thủ kho lập phiếu – lô tạm giữ", "Agreed" => "Mua hàng: NCC đồng ý nhận lại", "Refused" => "Mua hàng: NCC không nhận",
            "Posted" => "Thủ kho ghi sổ xuất trả", "Cancelled" => "Hủy phiếu", _ => a,
        };

        // ---------------------------------------------------------------- đọc

        private async Task<List<SupplierReturnModel>> MapAsync(List<SupplierIssue> issues, bool withHistory)
        {
            var db = _unitOfWork.DbContext;
            var pays = issues.ToDictionary(i => i.Id, i => Parse(i.Description));
            var lotIds = pays.Values.Where(p => p != null).Select(p => p!.Lot).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var ingIds = pays.Values.Where(p => p != null).Select(p => p!.Ingredient).Distinct().ToList();
            var ings = await db.Ingredient.AsNoTracking().Where(x => ingIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.Code, x.Name });
            var whIds = pays.Values.Where(p => p != null).Select(p => p!.Warehouse).Distinct().ToList();
            var whs = await db.Warehouse.AsNoTracking().Where(x => whIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Code);
            var unitIds = pays.Values.Where(p => p != null).Select(p => p!.Unit).Distinct().ToList();
            var units = await db.UnitMeasure.AsNoTracking().Where(x => unitIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Code);
            var supIds = issues.Select(i => i.SupplierId).Distinct().ToList();
            var sups = await db.Supplier.AsNoTracking().Where(x => supIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.Code, x.Name });
            var grIds = issues.Where(i => i.GoodsReceiptId != null).Select(i => i.GoodsReceiptId!.Value).Distinct().ToList();
            var grs = await db.GoodsReceipt.AsNoTracking().Where(x => grIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ReceiptNumber ?? x.ReceiptNo);
            var grlIds = lots.Values.Where(l => l.GoodsReceiptLineId != null).Select(l => l.GoodsReceiptLineId!.Value).Distinct().ToList();
            var received = await db.GoodsReceiptLine.AsNoTracking().Where(x => grlIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.AcceptedQuantity);
            var onHand = (await db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && b.MaterialLotId != null && lotIds.Contains(b.MaterialLotId.Value))
                .Select(b => new { Lot = b.MaterialLotId!.Value, b.OnHandQuantity }).ToListAsync()).GroupBy(b => b.Lot).ToDictionary(g => g.Key, g => g.Sum(x => x.OnHandQuantity));
            var ids = issues.Select(i => i.Id).ToList();
            var logs = (await db.AuditChange.AsNoTracking().Where(a => !a.IsDeleted && a.EntityName == Entity && a.IdChange != null && ids.Contains(a.IdChange.Value)).OrderBy(a => a.CreatedDate).ToListAsync()).ToLookup(a => a.IdChange!.Value);
            var userNames = await CommonAccess.UserNamesAsync(db, logs.SelectMany(g => g).Select(a => a.CreatedBy).Distinct());
            var reasons = Reasons();
            var list = new List<SupplierReturnModel>();
            foreach (var i in issues)
            {
                var p = pays[i.Id];
                if (p == null)
                {
                    continue;
                }
                var lot = lots.GetValueOrDefault(p.Lot);
                var ing = ings.GetValueOrDefault(p.Ingredient);
                var sup = sups.GetValueOrDefault(i.SupplierId);
                string state = StateOf(i, p);
                var hist = logs[i.Id].ToList();
                var m = new SupplierReturnModel
                {
                    Id = i.Id, Number = NumberOf(i), State = state, StateName = SupplierReturnState.Label(state), ReportedAt = i.ReportedAt,
                    SupplierId = i.SupplierId, SupplierCode = sup?.Code, SupplierName = sup?.Name, GoodsReceiptId = i.GoodsReceiptId,
                    GoodsReceiptNumber = i.GoodsReceiptId is { } g ? grs.GetValueOrDefault(g) : null, LinkedIssueId = p.LinkedIssue,
                    MaterialLotId = p.Lot, LotNo = lot?.LotNumber ?? lot?.LotNo, IngredientCode = ing?.Code, IngredientName = ing?.Name,
                    WarehouseName = whs.GetValueOrDefault(p.Warehouse), UnitName = units.GetValueOrDefault(p.Unit),
                    ReceivedQuantity = lot?.GoodsReceiptLineId is { } gl ? received.GetValueOrDefault(gl) : null, OnHand = onHand.GetValueOrDefault(p.Lot),
                    Quantity = p.Quantity, UnitCost = p.UnitCost, Amount = p.UnitCost is { } c ? Math.Round(c * p.Quantity, 0) : null,
                    ReasonCode = p.Reason, ReasonName = reasons.FirstOrDefault(r => r.Code == p.Reason)?.Label ?? p.Reason, Note = p.Note,
                    CreatedByName = hist.FirstOrDefault() is { } h0 ? userNames.GetValueOrDefault(h0.CreatedBy ?? Guid.Empty) : null,
                    PostedAt = p.PostedDate,
                    CanConfirm = state == SupplierReturnState.Pending, CanPost = state == SupplierReturnState.Agreed,
                    CanCancel = state is SupplierReturnState.Pending or SupplierReturnState.Agreed,
                };
                if (withHistory)
                {
                    m.ListHistory = [.. hist.Select(a => new SupplierReturnHistoryModel
                    {
                        Action = a.Action ?? string.Empty, ActionName = ActionName(a.Action ?? string.Empty), By = userNames.GetValueOrDefault(a.CreatedBy ?? Guid.Empty), At = a.CreatedDate,
                        Note = NoteOf(a.Changes),
                    })];
                }
                list.Add(m);
            }
            return list;
        }

        private static string? NoteOf(string? changes)
        {
            try
            {
                return changes == null ? null : Newtonsoft.Json.Linq.JObject.Parse(changes)["note"]?.ToString();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task<IResult<SupplierReturnListModel>> GetList(string? state, string? search, string? month)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime first = DateTime.TryParseExact(month ?? string.Empty, "yyyy-MM", Inv, DateTimeStyles.None, out var mm) ? mm : new DateTime(_clock.Today.Year, _clock.Today.Month, 1);
                DateTime from = first.AddMonths(-2), end = first.AddMonths(1);
                // Đang mở (chờ / đồng ý) hiện mọi thời điểm; đã đóng chỉ 3 tháng tới tháng chọn.
                var issues = await db.SupplierIssue.AsNoTracking().Where(i => !i.IsDeleted && i.IssueType == IssueType
                    && (i.IssueStatus == SupplierIssueStatus.Open || i.IssueStatus == SupplierIssueStatus.InProgress || (i.ReportedAt >= from && i.ReportedAt < end)))
                    .OrderByDescending(i => i.ReportedAt).Take(500).ToListAsync();
                var all = await MapAsync(issues, false);
                var model = new SupplierReturnListModel
                {
                    Pending = all.Count(x => x.State == SupplierReturnState.Pending), Agreed = all.Count(x => x.State == SupplierReturnState.Agreed),
                    ListReason = [.. Reasons().Select(r => new ReferenceOptionModel { Code = r.Code, Label = r.Label })],
                };
                var postedMonth = all.Where(x => x.State == SupplierReturnState.Posted && x.PostedAt is { } d && d >= first && d < end).ToList();
                model.PostedThisMonth = postedMonth.Count;
                model.PostedAmountThisMonth = postedMonth.Sum(x => x.Amount ?? 0m);
                string? q = search?.Trim();
                model.ListData = [.. all.Where(x => string.IsNullOrEmpty(state) || state == "all" || x.State == state || (state == "open" && x.CanCancel))
                    .Where(x => string.IsNullOrEmpty(q) || new[] { x.Number, x.SupplierName, x.SupplierCode, x.IngredientName, x.IngredientCode, x.LotNo, x.GoodsReceiptNumber }
                        .Any(s => s != null && s.Contains(q, StringComparison.OrdinalIgnoreCase)))];
                return Result<SupplierReturnListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<SupplierReturnModel>> GetDetail(Guid id)
        {
            var issue = await _unitOfWork.DbContext.SupplierIssue.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted && i.IssueType == IssueType);
            var m = issue == null ? null : (await MapAsync([issue], true)).FirstOrDefault();
            return m == null ? Result<SupplierReturnModel>.Fail(Msg.NotFound) : Result<SupplierReturnModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>Lô còn tồn để trả (gộp theo lô × kho; ưu tiên lô có NCC).</summary>
        public async Task<IResult<List<SupplierReturnLotModel>>> GetLots(string? search, Guid? supplierId)
        {
            var db = _unitOfWork.DbContext;
            var bal = await db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && b.MaterialLotId != null && b.OnHandQuantity > 0)
                .Select(b => new { Lot = b.MaterialLotId!.Value, b.WarehouseId, b.UnitId, b.OnHandQuantity }).ToListAsync();
            var lotIds = bal.Select(b => b.Lot).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => !l.IsDeleted && lotIds.Contains(l.Id) && (supplierId == null || l.SupplierId == supplierId)).ToDictionaryAsync(l => l.Id);
            var ingIds = lots.Values.Select(l => l.IngredientId).Distinct().ToList();
            var ings = await db.Ingredient.AsNoTracking().Where(x => ingIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.Code, x.Name });
            var sups = await db.Supplier.AsNoTracking().Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
            var whs = await db.Warehouse.AsNoTracking().Select(x => new { x.Id, x.Name, x.Code }).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Code);
            var units = await db.UnitMeasure.AsNoTracking().Select(x => new { x.Id, x.Name, x.Code }).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Code);
            var grlIds = lots.Values.Where(l => l.GoodsReceiptLineId != null).Select(l => l.GoodsReceiptLineId!.Value).ToList();
            var grl = await db.GoodsReceiptLine.AsNoTracking().Where(x => grlIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.GoodsReceiptId, x.AcceptedQuantity });
            var grIds = grl.Values.Select(x => x.GoodsReceiptId).Distinct().ToList();
            var grs = await db.GoodsReceipt.AsNoTracking().Where(x => grIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ReceiptNumber ?? x.ReceiptNo);
            string? q = search?.Trim();
            var list = bal.Where(b => lots.ContainsKey(b.Lot)).GroupBy(b => (b.Lot, b.WarehouseId)).Select(g =>
            {
                var l = lots[g.Key.Lot];
                var ing = ings.GetValueOrDefault(l.IngredientId);
                var line = l.GoodsReceiptLineId is { } gl ? grl.GetValueOrDefault(gl) : null;
                return new SupplierReturnLotModel
                {
                    MaterialLotId = l.Id, LotNo = l.LotNumber ?? l.LotNo, IngredientId = l.IngredientId, IngredientCode = ing?.Code, IngredientName = ing?.Name,
                    WarehouseId = g.Key.WarehouseId, WarehouseName = whs.GetValueOrDefault(g.Key.WarehouseId), UnitId = g.First().UnitId, UnitName = units.GetValueOrDefault(g.First().UnitId),
                    SupplierId = l.SupplierId, SupplierName = l.SupplierId is { } s ? sups.GetValueOrDefault(s) : null, GoodsReceiptId = line?.GoodsReceiptId,
                    GoodsReceiptNumber = line != null ? grs.GetValueOrDefault(line.GoodsReceiptId) : null, ReceivedQuantity = line?.AcceptedQuantity,
                    OnHand = g.Sum(x => x.OnHandQuantity), UnitCost = l.UnitCost, ExpiryDate = l.ExpiryDate, LotStatus = l.Status,
                };
            }).Where(x => string.IsNullOrEmpty(q) || new[] { x.LotNo, x.IngredientCode, x.IngredientName, x.SupplierName, x.GoodsReceiptNumber }.Any(s => s != null && s.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.SupplierId == null).ThenBy(x => x.IngredientName).ThenBy(x => x.ExpiryDate).Take(200).ToList();
            return Result<List<SupplierReturnLotModel>>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- ghi

        public async Task<IResult<SupplierReturnModel>> Create(SupplierReturnCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor().UserId;
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now;
                var lot = await db.MaterialLot.FirstOrDefaultAsync(l => l.Id == request.MaterialLotId && !l.IsDeleted);
                if (lot == null)
                {
                    return Fail(Msg.LotNotFound);
                }
                if (lot.SupplierId == null)
                {
                    return Fail(Msg.LotNoSupplier);
                }
                var reason = Reasons().FirstOrDefault(r => string.Equals(r.Code, request.Reason?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (reason == null)
                {
                    return Fail(Msg.ReasonRequired);
                }
                if (request.Quantity <= 0)
                {
                    return Fail(Msg.QuantityInvalid);
                }
                var rows = await db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && b.MaterialLotId == lot.Id && b.OnHandQuantity > 0
                    && (request.WarehouseId == null || b.WarehouseId == request.WarehouseId)).OrderByDescending(b => b.OnHandQuantity).ToListAsync();
                if (rows.Count == 0)
                {
                    return Fail(Msg.LotEmpty);
                }
                if (request.WarehouseId == null && rows.Select(r => r.WarehouseId).Distinct().Count() > 1)
                {
                    return Fail(Msg.ChooseWarehouse);
                }
                var row = rows[0];
                decimal held = await OpenQuantityAsync(lot.Id, null);
                if (request.Quantity > row.OnHandQuantity - held)
                {
                    return Fail(string.Format(Msg.OverStock, N(row.OnHandQuantity - held), N(row.OnHandQuantity), N(held)));
                }
                if (await AccountingPeriodRules.LockedMessageAsync(db, now, "lập phiếu trả hàng NCC") is { } locked)
                {
                    return Fail(locked);
                }
                var grLine = lot.GoodsReceiptLineId is { } gl ? await db.GoodsReceiptLine.AsNoTracking().FirstOrDefaultAsync(x => x.Id == gl) : null;
                var gr = grLine == null ? null : await db.GoodsReceipt.AsNoTracking().FirstOrDefaultAsync(x => x.Id == grLine.GoodsReceiptId);
                var ing = await db.Ingredient.AsNoTracking().Where(x => x.Id == lot.IngredientId).Select(x => new { x.Code, x.Name }).FirstOrDefaultAsync();
                string? note = DeliveryRules.Clean(request.Note);
                var p = new Payload
                {
                    Lot = lot.Id, Warehouse = row.WarehouseId, Location = row.StorageLocationId, Ingredient = lot.IngredientId, Unit = row.UnitId, Quantity = request.Quantity,
                    UnitCost = lot.UnitCost ?? grLine?.UnitCost, Reason = reason.Code, Note = note, LinkedIssue = request.LinkedIssueId, PrevLotStatus = lot.Status,
                };
                var issue = new SupplierIssue
                {
                    Id = Guid.NewGuid(), SupplierId = lot.SupplierId.Value, PurchaseOrderId = gr?.PurchaseOrderId, GoodsReceiptId = gr?.Id, IssueType = IssueType,
                    Severity = ReceivingCode.SeverityMedium, ReportedAt = now, IssueStatus = SupplierIssueStatus.Open, OwnerId = actor,
                };
                string text = $"Trả hàng NCC: {ing?.Code} {ing?.Name} – lô {lot.LotNumber ?? lot.LotNo} – {N(request.Quantity)} – {reason.Label}" + (note == null ? string.Empty : $" ({note})");
                issue.Description = Write(text, p);
                _unitOfWork.SupplierIssueRepository.Add(issue);
                lot.Status = MaterialLotStatus.OnHold;
                db.AuditChange.Add(Audit(issue.Id, "Created", note, actor, now));
                await db.SaveChangesAsync();
                _unitOfWork.CommitTransaction();
                await NotifyAsync(issue, $"↩️ Phiếu trả hàng NCC {NumberOf(issue)}: {text}. Cần Mua hàng xác nhận NCC đồng ý / không nhận.", ["OPS-SCM", "OPS-SCM-HEAD"], true);
                return Result<SupplierReturnModel>.Success((await GetDetail(issue.Id)).Data, string.Format(Msg.Created, NumberOf(issue)));
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<SupplierReturnModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            Result<SupplierReturnModel> Fail(string m)
            {
                _unitOfWork.RollbackTransaction();
                return Result<SupplierReturnModel>.Fail(m);
            }
        }

        public Task<IResult<SupplierReturnModel>> Confirm(SupplierReturnActionRequest request) => ActAsync(request, "Confirm");

        public Task<IResult<SupplierReturnModel>> Post(SupplierReturnActionRequest request) => ActAsync(request, "Post");

        public Task<IResult<SupplierReturnModel>> Cancel(SupplierReturnActionRequest request) => ActAsync(request, "Cancel");

        private async Task<IResult<SupplierReturnModel>> ActAsync(SupplierReturnActionRequest request, string op)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor().UserId;
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now;
                var issue = await db.SupplierIssue.FirstOrDefaultAsync(i => i.Id == request.Id && !i.IsDeleted && i.IssueType == IssueType);
                var p = issue == null ? null : Parse(issue.Description);
                if (issue == null || p == null)
                {
                    return Fail(Msg.NotFound);
                }
                string state = StateOf(issue, p);
                string? note = DeliveryRules.Clean(request.Note);
                string action;
                string? notice = null;
                string[] notify = ["OPS-WH"];
                switch (op)
                {
                    case "Confirm":
                        if (state != SupplierReturnState.Pending)
                        {
                            return Fail(string.Format(Msg.WrongState, SupplierReturnState.Label(state)));
                        }
                        if (request.Agree == null)
                        {
                            return Fail(Msg.AgreeRequired);
                        }
                        if (request.Agree == true)
                        {
                            issue.IssueStatus = SupplierIssueStatus.InProgress;
                            action = "Agreed";
                            notice = $"✅ {NumberOf(issue)}: NCC đồng ý nhận lại – giao trả và ghi sổ ở Kho › Trả hàng NCC.";
                        }
                        else
                        {
                            if (note == null)
                            {
                                return Fail(Msg.RefuseNoteRequired);
                            }
                            issue.IssueStatus = SupplierIssueStatus.Closed;
                            issue.ClosedAt = now;
                            p.State = SupplierReturnState.Refused;
                            await ReleaseLotAsync(p, issue.Id);
                            action = "Refused";
                            notice = $"⛔ {NumberOf(issue)}: NCC không nhận lại ({note}) – lô đã bỏ tạm giữ.";
                        }
                        break;
                    case "Post":
                        if (state != SupplierReturnState.Agreed)
                        {
                            return Fail(string.Format(Msg.WrongState, SupplierReturnState.Label(state)));
                        }
                        DateTime date = (request.Date ?? now).Date == now.Date ? now : (request.Date ?? now);
                        if (date > now)
                        {
                            return Fail(Msg.FutureDate);
                        }
                        if (await AccountingPeriodRules.LockedMessageAsync(db, date, "ghi sổ trả hàng NCC") is { } locked)
                        {
                            return Fail(locked);
                        }
                        var txn = await _ledger.PostAsync(new InventoryPostRequest
                        {
                            TransactionType = "RETURN", TransactionNumber = NumberOf(issue), WarehouseId = p.Warehouse, StorageLocationId = p.Location, IngredientId = p.Ingredient,
                            MaterialLotId = p.Lot, UnitId = p.Unit, QuantityOut = p.Quantity, UnitCost = p.UnitCost, ReferenceType = ReferenceType, ReferenceId = issue.Id,
                            Reason = Reasons().FirstOrDefault(r => r.Code == p.Reason)?.Label ?? p.Reason, Note = note ?? p.Note, TransactionDate = date,
                        });
                        issue.IssueStatus = SupplierIssueStatus.Resolved;
                        issue.Resolution = $"[{IssueType}] Đã trả {N(p.Quantity)} – {NumberOf(issue)}";
                        p.Transaction = txn.Id;
                        p.PostedDate = date;
                        await ReleaseLotAsync(p, issue.Id);
                        action = "Posted";
                        notify = ["OPS-SCM", "OPS-SCM-HEAD", "FIN-ACC"];
                        notice = $"📦 {NumberOf(issue)}: đã ghi sổ xuất trả NCC {N(p.Quantity)}" + (p.UnitCost is { } c ? $" – {N(Math.Round(c * p.Quantity, 0))} đ" : string.Empty)
                            + ". Kế toán: chứng từ Trả lại hàng mua ở Chứng từ kế toán.";
                        break;
                    default:
                        if (state is not (SupplierReturnState.Pending or SupplierReturnState.Agreed))
                        {
                            return Fail(string.Format(Msg.WrongState, SupplierReturnState.Label(state)));
                        }
                        if (note == null)
                        {
                            return Fail(Msg.CancelNoteRequired);
                        }
                        issue.IssueStatus = SupplierIssueStatus.Closed;
                        issue.ClosedAt = now;
                        p.State = SupplierReturnState.Cancelled;
                        await ReleaseLotAsync(p, issue.Id);
                        action = "Cancelled";
                        notify = ["OPS-SCM", "OPS-SCM-HEAD"];
                        notice = $"🗑 {NumberOf(issue)} đã hủy ({note}) – lô bỏ tạm giữ.";
                        break;
                }
                issue.Description = Write(HeadOf(issue.Description), p);
                _unitOfWork.SupplierIssueRepository.Update(issue);
                db.AuditChange.Add(Audit(issue.Id, action, note, actor, now));
                await db.SaveChangesAsync();
                _unitOfWork.CommitTransaction();
                if (notice != null)
                {
                    await NotifyAsync(issue, notice, notify);
                }
                return Result<SupplierReturnModel>.Success((await GetDetail(issue.Id)).Data, ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<SupplierReturnModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", op, ": ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            Result<SupplierReturnModel> Fail(string m)
            {
                _unitOfWork.RollbackTransaction();
                return Result<SupplierReturnModel>.Fail(m);
            }
        }

        /// <summary>Số đang giữ cho phiếu trả khác còn mở của lô (chờ / đồng ý).</summary>
        private async Task<decimal> OpenQuantityAsync(Guid lotId, Guid? exceptIssue)
        {
            var open = await _unitOfWork.DbContext.SupplierIssue.AsNoTracking().Where(i => !i.IsDeleted && i.IssueType == IssueType && i.Id != exceptIssue
                && (i.IssueStatus == SupplierIssueStatus.Open || i.IssueStatus == SupplierIssueStatus.InProgress)).Select(i => i.Description).ToListAsync();
            return open.Select(Parse).Where(p => p != null && p.Lot == lotId).Sum(p => p!.Quantity);
        }

        /// <summary>Bỏ tạm giữ lô khi không còn phiếu trả mở khác: hết tồn → Consumed, còn tồn → trạng thái trước khi giữ (mặc định Available).</summary>
        private async Task ReleaseLotAsync(Payload p, Guid issueId)
        {
            var db = _unitOfWork.DbContext;
            var lot = await db.MaterialLot.FirstOrDefaultAsync(l => l.Id == p.Lot);
            if (lot == null || lot.Status != MaterialLotStatus.OnHold || await OpenQuantityAsync(p.Lot, issueId) > 0)
            {
                return;
            }
            decimal left = await db.InventoryBalance.Where(b => !b.IsDeleted && b.MaterialLotId == p.Lot).SumAsync(b => (decimal?)b.OnHandQuantity) ?? 0m;
            lot.Status = left <= 0 ? MaterialLotStatus.Consumed
                : p.PrevLotStatus is null or MaterialLotStatus.OnHold ? MaterialLotStatus.Available : p.PrevLotStatus;
        }

        private async Task NotifyAsync(SupplierIssue issue, string text, string[] groups, bool confirmButtons = false)
        {
            try
            {
                var users = await RecipientResolver.UsersInGroupsAsync(_unitOfWork.DbContext, groups);
                if (users.Count == 0)
                {
                    return;
                }
                await BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotVanHanh,
                    Data = new { text = text + $"\nMở Kho › Trả hàng NCC: #/{ScreenId}?id={issue.Id}" },
                    // ACC-5: Mua hàng bấm thẳng trên bot (ThaoTacActions op:sra / op:srr – quyền Sửa #/scm-supplier-return).
                    Buttons = confirmButtons ? new[] { new NotificationButton("✅ NCC đồng ý", BusinessNotifier.Callback("op", "sra", issue.Id)), new NotificationButton("⛔ NCC không nhận", BusinessNotifier.Callback("op", "srr", issue.Id)) } : null,
                    IdempotencyKey = $"SUPPLIER_RETURN:{issue.Id:N}:{issue.IssueStatus}",
                });
            }
            catch (Exception ex)
            {
                Log.Warning(string.Concat("[", GetType().Name, "]", " Notify: ", ex.Message));
            }
        }

        private static string N(decimal v) => v.ToString("#,##0.###", CultureInfo.GetCultureInfo("vi-VN"));

        public static class Msg
        {
            public const string NotFound = "Không tìm thấy phiếu trả hàng NCC";
            public const string LotNotFound = "Không tìm thấy lô nguyên liệu";
            public const string LotNoSupplier = "Lô này không gắn nhà cung cấp – không lập phiếu trả được";
            public const string LotEmpty = "Lô đã hết tồn – không còn gì để trả";
            public const string ChooseWarehouse = "Lô đang có ở nhiều kho – chọn kho trả hàng";
            public const string ReasonRequired = "Chọn lý do trả hàng";
            public const string QuantityInvalid = "Số trả phải lớn hơn 0";
            public const string OverStock = "Số trả vượt tồn còn trả được của lô: tối đa {0} (tồn {1}, đang chờ trả ở phiếu khác {2})";
            public const string Created = "Đã lập phiếu trả {0} – lô chuyển Tạm giữ, đã báo Mua hàng";
            public const string WrongState = "Phiếu đang ở trạng thái «{0}» – không thực hiện được thao tác này";
            public const string AgreeRequired = "Chọn NCC đồng ý hay không nhận";
            public const string RefuseNoteRequired = "Ghi lý do NCC không nhận";
            public const string CancelNoteRequired = "Ghi lý do hủy phiếu";
            public const string FutureDate = "Ngày ghi sổ không được sau hôm nay";
            public const string ManagedHere = "Phiếu trả hàng NCC xử lý ở Kho › Trả hàng NCC";
        }
    }
}
