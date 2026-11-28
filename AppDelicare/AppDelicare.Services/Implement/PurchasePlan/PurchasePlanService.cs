using AppDelicare.Data.Repositories;
using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>
    /// UI-6.51 (D-137): kế hoạch mua tự động.
    /// <list type="number">
    /// <item><b>Lập / cập nhật đề nghị</b> (job 14:00 + 20:15, nút "Tính lại ngay" / "Đặt phần hôm nay" / "Đặt ngay"): tính kế hoạch mua 7 ngày (nguồn như Kế hoạch mua
    ///   7 ngày, trừ tồn + PO sắp về + dòng đề nghị đang mở – KHÔNG trừ đề nghị Nháp của chính kế hoạch vì sẽ ghi đè), nguyên liệu đến hạn đặt → một đề nghị / ngày cần:
    ///   còn Nháp → cập nhật dòng; đã gửi duyệt → phần chênh lập đề nghị bổ sung. Quyết định thay thế của ngày được lưu (sản xuất áp đúng tỷ lệ đã mua).</item>
    /// <item><b>Duyệt theo rủi ro</b> (đề nghị do hệ thống lập): đúng định mức → tự duyệt + tự lập / tự duyệt PO; vượt → gửi duyệt 1 cấp Trưởng mua hàng
    ///   (≥ ngưỡng Giám đốc thêm COO – PurchaseRequestService.ApprovalStepScreenIdsAsync). Đề nghị lập tay giữ 2 cấp.</item>
    /// <item><b>Tự gửi NCC</b> lúc <c>PurchasePlan:SendTime</c> (sau giờ đó gửi ngay khi duyệt) qua <see cref="ISupplierOrderSendService"/>; lỗi / không kênh → chờ gửi tay + bot.</item>
    /// </list>
    /// </summary>
    public sealed partial class PurchasePlanService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules, IOptions<PurchasePlanOptions> plan,
        IMaterialRequirementService materialRequirementService, IPurchaseRequestService purchaseRequestService, IPurchaseOrderService purchaseOrderService,
        IDocumentNumberService documentNumberService, IUomConversionService uomConversionService, ISupplierOrderSendService sender, INotificationService notifications) : IPurchasePlanService
    {
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly PurchasePlanOptions _plan = plan.Value;
        private static readonly string[] BuyerMenus = ["#/scm-pr", "#/scm-po"];

        private static string Day(DateTime d) => d.ToString("dd/MM", CultureInfo.InvariantCulture);

        private static string Qty(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private static decimal Ceil4(decimal v) => Math.Ceiling(v * 10000m) / 10000m;

        // ------------------------------------------------------------------ chạy

        public async Task<IResult<PurchasePlanRunResultModel>> Run(PurchasePlanRunRequest request)
        {
            try
            {
                var (result, error) = await RunCoreAsync(request, "Bấm tay");
                return error != null ? Result<PurchasePlanRunResultModel>.Fail(error)
                    : Result<PurchasePlanRunResultModel>.Success(result!, result!.Created + result.Updated == 0 ? "Không có phần nào cần đặt thêm" : result.Summary);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Run: ", ex.ToString()));
                throw;
            }
        }

        public async Task<PurchasePlanRunResultModel> RunJobAsync(bool first)
        {
            var (result, error) = await RunCoreAsync(new PurchasePlanRunRequest { OnlyDue = true }, first ? "Job " + _plan.RunTime(0) : "Job " + _plan.RunTime(1));
            if (error != null)
            {
                throw new InvalidOperationException(error);
            }
            await AlertAfterRunAsync(first);
            return result!;
        }

        private async Task<(PurchasePlanRunResultModel? Result, string? Error)> RunCoreAsync(PurchasePlanRunRequest request, string trigger)
        {
            var db = unitOfWork.DbContext;
            DateTime today = clock.Today;
            var result = new PurchasePlanRunResultModel();
            var drafts = await db.PurchaseRequest.Where(p => !p.IsDeleted && p.Status == PurchaseRequestStatus.Draft && p.Note != null && p.Note.StartsWith(PurchasePlanRules.Marker)).ToListAsync();
            var (model, error) = await materialRequirementService.PlanHorizonAsync(new MaterialHorizonRequest { OrganizationUnitId = request.OrganizationUnitId }, [.. drafts.Select(d => d.Id)]);
            if (model == null)
            {
                return (null, error);
            }
            bool single = request.IngredientId != null;
            var targets = new Dictionary<DateTime, Dictionary<Guid, (decimal Qty, MaterialHorizonItemModel Item, MaterialHorizonCellModel Cell)>>();
            foreach (var item in model.ListItem)
            {
                foreach (var cell in item.ListDay.Where(c => c.Shortage > 0))
                {
                    bool due = (cell.OrderByDate ?? cell.Date) <= today;
                    bool take = single ? item.IngredientId == request.IngredientId && (request.NeedDate == null || request.NeedDate.Value.Date == cell.Date.Date)
                        : !request.OnlyDue || due;
                    if (take)
                    {
                        (targets.TryGetValue(cell.Date.Date, out var t) ? t : targets[cell.Date.Date] = [])[item.IngredientId] = (Ceil4(cell.Shortage), item, cell);
                    }
                }
            }
            var dates = targets.Keys.ToHashSet();
            if (!single)
            {
                dates.UnionWith(drafts.Where(d => d.RequiredDate != null).Select(d => d.RequiredDate!.Value.Date));
            }
            var touched = new List<Guid>();
            foreach (DateTime date in dates.Order())
            {
                var want = targets.GetValueOrDefault(date) ?? [];
                var (prId, created, changed) = await UpsertDayAsync(date, want, drafts.FirstOrDefault(d => d.RequiredDate?.Date == date), single, model, request.Note, trigger);
                if (prId is { } id && want.Count > 0)
                {
                    touched.Add(id);
                }
                result.Created += created ? 1 : 0;
                result.Updated += !created && changed ? 1 : 0;
            }
            foreach (Guid id in touched.Distinct())
            {
                var pr = await db.PurchaseRequest.AsNoTracking().FirstAsync(p => p.Id == id);
                result.ListRequestNumber.Add(pr.RequestNumber ?? pr.RequestNo ?? string.Empty);
                if (_plan.AutoSubmit && pr.Status == PurchaseRequestStatus.Draft)
                {
                    await ProcessAsync(id, result);
                }
            }
            if (_plan.AutoSend && clock.Now.TimeOfDay >= PurchasePlanRules.ParseTime(_plan.SendTime, new TimeSpan(15, 0, 0)))
            {
                foreach (Guid id in touched.Distinct())
                {
                    result.Sent += await SendForRequestAsync(id);
                }
            }
            db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = RunKey, EntityName = "PurchasePlanRun", Action = trigger, CreatedDate = clock.Now, CreatedBy = currentActor.Actor?.UserId, IsDeleted = false,
                Changes = JsonConvert.SerializeObject(new { at = clock.Now, trigger, result.Summary, requests = result.ListRequestNumber }),
            });
            await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
            return (result, null);
        }

        private static readonly Guid RunKey = Guid.Parse("6a51b0a1-0000-4000-8000-0000000000aa");

        /// <summary>Một ngày cần: cập nhật đề nghị Nháp của kế hoạch (dòng = phần phải đặt; không còn → xóa mềm dòng / hủy đề nghị rỗng) hoặc lập mới (bổ sung nếu đã có đề nghị gửi duyệt).</summary>
        private async Task<(Guid? PrId, bool Created, bool Changed)> UpsertDayAsync(DateTime date, Dictionary<Guid, (decimal Qty, MaterialHorizonItemModel Item, MaterialHorizonCellModel Cell)> want,
            PurchaseRequest? draft, bool single, MaterialHorizonModel model, string? note, string trigger)
        {
            var db = unitOfWork.DbContext;
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                DateTime now = clock.Now;
                Guid? by = currentActor.Actor?.UserId;
                bool created = false, changed = false;
                var lines = draft == null ? [] : await db.PurchaseRequestLine.Where(l => l.PurchaseRequestId == draft.Id && !l.IsDeleted).ToListAsync();
                if (draft == null && want.Count == 0)
                {
                    unitOfWork.RollbackTransaction();
                    return (null, false, false);
                }
                if (draft == null)
                {
                    bool supplement = await db.PurchaseRequest.AnyAsync(p => !p.IsDeleted && p.RequiredDate == date && p.Note != null && p.Note.StartsWith(PurchasePlanRules.Marker)
                        && p.Status != PurchaseRequestStatus.Draft && p.Status != PurchaseRequestStatus.Cancelled && p.Status != PurchaseRequestStatus.Rejected);
                    string number = await documentNumberService.NextAsync(DocumentKind.PurchaseRequest, clock.Today);
                    draft = new PurchaseRequest
                    {
                        Id = Guid.NewGuid(), RequestNumber = number, RequestNo = number, RequestDate = now, RequiredDate = date, Status = PurchaseRequestStatus.Draft,
                        Note = $"{PurchasePlanRules.Marker} Ngày cần {date:dd/MM/yyyy}{(supplement ? " – BỔ SUNG phần tăng thêm" : string.Empty)} ({trigger})"
                            + (string.IsNullOrWhiteSpace(note) ? string.Empty : " – " + note.Trim()),
                    };
                    db.PurchaseRequest.Add(draft);
                    created = true;
                }
                foreach (var line in lines.Where(l => !want.ContainsKey(l.IngredientId) && !single))
                {
                    line.IsDeleted = true;
                    line.DeletedDate = now;
                    line.DeletedBy = by;
                    changed = true;
                }
                foreach (var (ingredientId, (qty, item, cell)) in want)
                {
                    string lineNote = $"{PurchasePlanRules.ComputedTag(qty)} " + (item.SupplierName == null ? "Chưa có NCC đạt" : $"NCC gợi ý: {item.SupplierName}")
                        + (cell.OrderByDate is { } ob ? $" – đặt trước {ob:dd/MM}" : string.Empty)
                        + SubstituteNote(model, date, ingredientId);
                    var line = lines.FirstOrDefault(l => l.IngredientId == ingredientId);
                    if (line == null)
                    {
                        db.PurchaseRequestLine.Add(new PurchaseRequestLine
                        {
                            Id = Guid.NewGuid(), PurchaseRequestId = draft.Id, IngredientId = ingredientId, Quantity = qty, UnitId = item.UnitId, UomId = item.UnitId, RequiredDate = date, Note = lineNote,
                        });
                        changed = true;
                    }
                    else if (line.Quantity != qty || line.Note != lineNote)
                    {
                        line.Quantity = qty;
                        line.Note = lineNote;
                        changed = true;
                    }
                }
                if (!created && !single && want.Count == 0)
                {
                    draft.Status = PurchaseRequestStatus.Cancelled;
                    draft.Note = PurchasingRules.AppendNote(draft.Note, "Hủy: không còn phần phải đặt (tồn / đơn mua đã đủ)");
                    changed = true;
                }
                // Lưu quyết định thay thế của ngày (cặp có nguyên liệu trong đề nghị) – sản xuất / phiếu xuất áp đúng tỷ lệ đã mua.
                foreach (var s in model.ListSubstitution.Where(s => s.Date.Date == date && (want.ContainsKey(s.StdIngredientId) || want.ContainsKey(s.SubIngredientId))))
                {
                    await SubstitutionRules.SaveAsync(db, DecisionOf(s), SubstitutionRules.ActionDecide, by, now);
                }
                await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
                unitOfWork.CommitTransaction();
                return (draft.Id, created, changed);
            }
            catch
            {
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private static string SubstituteNote(MaterialHorizonModel model, DateTime date, Guid ingredientId)
        {
            var s = model.ListSubstitution.FirstOrDefault(x => x.Date.Date == date && (x.StdIngredientId == ingredientId || x.SubIngredientId == ingredientId) && x.ListDish.Any(d => d.SubGross > 0));
            if (s == null)
            {
                return string.Empty;
            }
            string over = s.Source == "OVERRIDE" && s.SharePct > (s.MaxSharePct ?? 0m) ? " " + OverShareTag : string.Empty;
            return $" – thay thế: {s.SubIngredientName} {s.SharePct:0.#}% thay {s.StdIngredientName}" + over;
        }

        /// <summary>Thẻ trên ghi chú dòng: người mua đổi tỷ lệ thay thế vượt % tối đa của công thức → không tự duyệt.</summary>
        public const string OverShareTag = "[vượt tỷ lệ thay thế của công thức]";

        internal static SubstitutionRules.Decision DecisionOf(MaterialHorizonSubstitutionModel s) => new()
        {
            Date = s.Date.Date, StdIngredientId = s.StdIngredientId, SubIngredientId = s.SubIngredientId, Group = s.Group, SharePct = s.SharePct, MaxSharePct = s.MaxSharePct,
            StdUnitPrice = s.StdUnitPrice, SubUnitPrice = s.SubUnitPrice, StdUsablePrice = s.StdUsablePrice, SubUsablePrice = s.SubUsablePrice, StdWastePct = s.StdWastePct,
            SubWastePct = s.SubWastePct, SavingPct = s.SavingPct, StdSupplierName = s.StdSupplierName, SubSupplierName = s.SubSupplierName, Source = s.Source ?? "AUTO", Reason = s.Reason,
            StdOnlyByDish = s.ListDish.GroupBy(d => d.DishId).ToDictionary(g => g.Key, g => g.First().Portions > 0 ? Math.Round(g.First().StdOnlyPortions / g.First().Portions, 6) : 0m),
            SubQuantity = s.ListDish.Sum(d => d.SubGross), SavingAmount = s.SavingAmount,
        };

        // ------------------------------------------------------------------ duyệt theo rủi ro

        /// <summary>Kiểm định mức đề nghị do hệ thống lập: trả danh sách lý do vượt (rỗng = đúng định mức) + tổng ước tính.</summary>
        public async Task<(List<string> Reasons, decimal Total)> EvaluateNormAsync(Guid purchaseRequestId)
        {
            var db = unitOfWork.DbContext;
            var (total, lines) = await PurchasePlanRules.EstimateAsync(db, uomConversionService, _rules, _plan, clock.Today, purchaseRequestId);
            var ingIds = lines.Select(x => x.Line.IngredientId).Distinct().ToList();
            var names = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name ?? i.Code ?? string.Empty);
            var reasons = new List<string>();
            foreach (var (line, pick) in lines)
            {
                string name = names.GetValueOrDefault(line.IngredientId) ?? string.Empty;
                decimal? computed = PurchasePlanRules.ComputedOf(line.Note);
                if (computed == null)
                {
                    reasons.Add($"{name}: dòng thêm tay");
                }
                else if (computed > 0 && line.Quantity > computed.Value * (1m + _plan.TolerancePct / 100m) + 0.0001m)
                {
                    reasons.Add($"{name}: vượt định mức {Math.Round((line.Quantity / computed.Value - 1m) * 100m, 0)}%");
                }
                if (pick.SupplierId == null)
                {
                    reasons.Add($"{name}: chưa có NCC đạt ({pick.Reason})");
                }
                if (pick.SplitManual)
                {
                    reasons.Add($"{name}: người mua sửa phần chia NCC");
                }
                var up = pick.ListCard.Where(c => c.Suggested && c.ChangePct >= _rules.ApprovalFlagPricePct).Select(c => c.ChangePct).DefaultIfEmpty(null).Max();
                if (up != null)
                {
                    reasons.Add($"{name}: giá NCC tăng {up:0.#}%");
                }
                if (line.Note?.Contains(OverShareTag, StringComparison.Ordinal) == true)
                {
                    reasons.Add($"{name}: tỷ lệ thay thế vượt mức công thức");
                }
            }
            decimal cap = _rules.PurchaseOrderAutoApproveMaxAmount > 0 ? _rules.PurchaseOrderAutoApproveMaxAmount : _rules.PurchaseOrderDirectorApprovalAmount;
            if (total >= cap)
            {
                reasons.Add($"tổng {PurchasingRules.Money(total)} ≥ ngưỡng tự duyệt {PurchasingRules.Money(cap)}");
            }
            return (reasons, total);
        }

        /// <summary>Đúng định mức → tự duyệt + tự lập / duyệt PO; vượt → gửi duyệt (số cấp theo PurchaseRequestService.ApprovalStepScreenIdsAsync).</summary>
        private async Task ProcessAsync(Guid purchaseRequestId, PurchasePlanRunResultModel result)
        {
            var db = unitOfWork.DbContext;
            var (reasons, _) = await EvaluateNormAsync(purchaseRequestId);
            var pr = await db.PurchaseRequest.FirstAsync(p => p.Id == purchaseRequestId);
            string number = pr.RequestNumber ?? pr.RequestNo ?? string.Empty;
            if (reasons.Count == 0)
            {
                using (var tran = await unitOfWork.OpenTransaction())
                {
                    pr.Status = PurchaseRequestStatus.Approved;
                    pr.Note = PurchasingRules.AppendNote(pr.Note, PurchasePlanRules.AutoApprovedNote);
                    db.AuditChange.Add(new AuditChange
                    {
                        Id = Guid.NewGuid(), IdChange = pr.Id, EntityName = "PurchaseRequest", Action = "AutoApprove", CreatedDate = clock.Now, CreatedBy = currentActor.Actor?.UserId, IsDeleted = false,
                        Changes = JsonConvert.SerializeObject(new { note = PurchasePlanRules.AutoApprovedNote, tolerancePct = _plan.TolerancePct }),
                    });
                    await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
                    unitOfWork.CommitTransaction();
                }
                result.AutoApproved++;
                var conv = await purchaseRequestService.ConvertToPurchaseOrder(new PurchaseRequestConvertRequest { Id = pr.Id, Note = "Kế hoạch mua tự động" });
                if (conv.ResponseCode == 200 && conv.Data != null)
                {
                    result.PurchaseOrders += conv.Data.ListPurchaseOrder.Count;
                    result.ListMessage.AddRange(conv.Data.ListWarning);
                }
                else
                {
                    result.ListMessage.Add($"{number}: chưa lập được đơn mua – {conv.ResponseMessage}");
                }
                return;
            }
            var sub = await purchaseRequestService.Submit(new PurchaseActionRequest { Id = pr.Id, Note = "Cần duyệt: " + string.Join("; ", reasons.Take(5)) });
            if (sub.ResponseCode == 200)
            {
                result.Submitted++;
            }
            else
            {
                result.ListMessage.Add($"{number}: chưa gửi duyệt được – {sub.ResponseMessage}");
            }
        }

        // ------------------------------------------------------------------ gửi NCC

        public async Task<int> SendForRequestAsync(Guid purchaseRequestId)
        {
            var db = unitOfWork.DbContext;
            var ids = await db.PurchaseOrder.AsNoTracking().Where(o => o.PurchaseRequestId == purchaseRequestId && !o.IsDeleted && o.Status == PurchaseOrderStatus.Approved).Select(o => o.Id).ToListAsync();
            int sent = 0;
            foreach (Guid id in ids)
            {
                sent += await SendOneAsync(id) ? 1 : 0;
            }
            return sent;
        }

        private async Task<bool> SendOneAsync(Guid purchaseOrderId)
        {
            var db = unitOfWork.DbContext;
            var po = await db.PurchaseOrder.AsNoTracking().FirstAsync(o => o.Id == purchaseOrderId);
            string number = po.PurchaseOrderNumber ?? po.PONo ?? string.Empty;
            var r = await sender.SendAsync(purchaseOrderId);
            if (r.Ok)
            {
                var s = await purchaseOrderService.Send(new PurchaseActionRequest { Id = purchaseOrderId, Note = $"Tự gửi NCC qua {r.Channel}" });
                return s.ResponseCode == 200;
            }
            // Không kênh / lỗi → giữ "Chờ gửi NCC" (Đã duyệt), bot báo Mua hàng (1 tin / đơn / ngày).
            var supplier = await db.Supplier.AsNoTracking().Where(x => x.Id == po.SupplierId).Select(x => x.Name).FirstOrDefaultAsync();
            await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.PurchaseSendFailed, BuyerMenus, new
            {
                title = $"Đơn mua {number} ({supplier}) chưa gửi được cho NCC",
                detail = (r.NotConfigured ? "Chờ gửi tay: " : "Gửi lỗi: ") + r.Message,
                lines = new[] { $"📦 Ngày giao {(po.ExpectedDeliveryDate ?? po.ExpectedDate):dd/MM} · {PurchasingRules.Money(po.TotalAmount ?? 0m)}" },
            }, $"{NotificationCatalog.PurchaseSendFailed}:{po.Id:N}:{clock.Today:yyyyMMdd}", requireAdd: true);
            return false;
        }

        public async Task<(int Sent, int Failed, int Unconfirmed)> SendDueAsync()
        {
            var db = unitOfWork.DbContext;
            int sent = 0, failed = 0;
            if (_plan.AutoSend)
            {
                var ids = await (from o in db.PurchaseOrder.AsNoTracking()
                                 join p in db.PurchaseRequest.AsNoTracking() on o.PurchaseRequestId equals p.Id
                                 where !o.IsDeleted && o.Status == PurchaseOrderStatus.Approved && p.Note != null && p.Note.StartsWith(PurchasePlanRules.Marker)
                                 select o.Id).ToListAsync();
                foreach (Guid id in ids)
                {
                    if (await SendOneAsync(id)) sent++; else failed++;
                }
            }
            // Đơn đã gửi, sắp tới giờ giao (< SupplierConfirmHours) mà chưa nhận hàng → nhắc Mua hàng gọi NCC xác nhận (chưa có cột NCC xác nhận – GAP-PUR-CONFIRM).
            DateTime limit = clock.Now.AddHours(Math.Max(1, _plan.SupplierConfirmHours));
            var soon = await db.PurchaseOrder.AsNoTracking()
                .Where(o => !o.IsDeleted && o.Status == PurchaseOrderStatus.Sent && (o.ExpectedDeliveryDate ?? o.ExpectedDate) != null && (o.ExpectedDeliveryDate ?? o.ExpectedDate) <= limit
                    && (o.ExpectedDeliveryDate ?? o.ExpectedDate) >= clock.Today).ToListAsync();
            if (soon.Count > 0)
            {
                var supIds = soon.Select(o => o.SupplierId).Distinct().ToList();
                var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
                await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.PurchaseSupplierUnconfirmed, BuyerMenus, new
                {
                    title = $"{soon.Count} đơn mua sắp tới giờ giao – gọi NCC xác nhận",
                    lines = soon.Take(10).Select(o => $"📞 {o.PurchaseOrderNumber ?? o.PONo} · {sups.GetValueOrDefault(o.SupplierId)} · giao {(o.ExpectedDeliveryDate ?? o.ExpectedDate):dd/MM}").ToList(),
                }, $"{NotificationCatalog.PurchaseSupplierUnconfirmed}:{clock.Today:yyyyMMdd}", requireAdd: true);
            }
            return (sent, failed, soon.Count);
        }

        // ------------------------------------------------------------------ bot sau lần chạy

        /// <summary>Sau lần chạy: nguyên liệu tới hạn / quá hạn đặt mà chưa có đơn mua (lần 14:00) → Mua hàng; ngày có thay thế → bếp trưởng (1 tin / ngày cần).</summary>
        private async Task AlertAfterRunAsync(bool first)
        {
            var db = unitOfWork.DbContext;
            var board = await BuildBoardAsync(new PurchasePlanBoardRequest(), false);
            if (board == null)
            {
                return;
            }
            var open = board.ListRow.SelectMany(r => r.ListCell.Where(c => c.Status is "DUE_TODAY" or "LATE" or "DRAFT" or "NEED_PICK").Select(c => (r, c))).ToList();
            if (first && open.Count > 0)
            {
                await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.PurchaseDueNoOrder, BuyerMenus, new
                {
                    title = $"{open.Select(x => x.r.IngredientId).Distinct().Count()} nguyên liệu tới hạn đặt chưa có đơn mua",
                    lines = open.Take(12).Select(x => $"{(x.c.Status == "LATE" ? "🔴" : "🟠")} {x.r.IngredientName}: {Qty(x.c.Quantity)} {x.r.UnitName} cần {Day(x.c.Date)} – {x.c.StatusName}").ToList(),
                }, $"{NotificationCatalog.PurchaseDueNoOrder}:{clock.Today:yyyyMMdd}", requireAdd: true);
            }
            var model = await materialRequirementService.PlanHorizonAsync(new MaterialHorizonRequest());
            foreach (var g in (model.Model?.ListSubstitution ?? []).Where(s => s.Stored && s.ListDish.Any(d => d.SubGross > 0)).GroupBy(s => s.Date.Date))
            {
                await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.PurchaseSubstitution, ["#/menu-recipe"], new
                {
                    title = $"Ngày {Day(g.Key)} có dùng nguyên liệu thay thế",
                    lines = g.SelectMany(s => s.ListDish.Where(d => d.SubGross > 0).Select(d => $"⇄ {d.DishName}: {s.StdIngredientName} {Qty(d.StdGross)} + {s.SubIngredientName} {Qty(d.SubGross)}")).Take(12).ToList(),
                }, $"{NotificationCatalog.PurchaseSubstitution}:{g.Key:yyyyMMdd}", requireAdd: true);
            }
        }

        // ------------------------------------------------------------------ người mua sửa

        public async Task<IResult<bool>> OverrideSubstitute(PurchasePlanOverrideSubstituteRequest request)
        {
            if (request.SharePct is < 0 or > 100)
            {
                return Result<bool>.Fail("Tỷ lệ thay thế phải từ 0 đến 100%", false);
            }
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Result<bool>.Fail("Nhập lý do giữ chuẩn / đổi tỷ lệ", false);
            }
            var db = unitOfWork.DbContext;
            var (model, error) = await materialRequirementService.PlanHorizonAsync(new MaterialHorizonRequest { FromDate = request.NeedDate.Date, Days = 1 });
            var s = model?.ListSubstitution.FirstOrDefault(x => x.StdIngredientId == request.StdIngredientId && x.SubIngredientId == request.SubIngredientId);
            if (s == null)
            {
                return Result<bool>.Fail(error ?? $"Ngày {request.NeedDate:dd/MM/yyyy} không có món dùng cặp nguyên liệu thay thế này", false);
            }
            var d = DecisionOf(s);
            d.SharePct = request.SharePct;
            d.Source = "OVERRIDE";
            d.Reason = (request.SharePct == 0 ? "Người mua giữ 100% nguyên liệu chuẩn" : $"Người mua đổi tỷ lệ {request.SharePct:0.#}%") + " – " + request.Reason!.Trim();
            await SubstitutionRules.SaveAsync(db, d, SubstitutionRules.ActionOverride, currentActor.Actor?.UserId, clock.Now);
            await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, request.SharePct == 0 ? $"Đã giữ 100% {s.StdIngredientName} ngày {request.NeedDate:dd/MM} – bấm Tính lại ngay để cập nhật đề nghị" : $"Đã đổi tỷ lệ {s.SubIngredientName} {request.SharePct:0.#}% ngày {request.NeedDate:dd/MM}");
        }

        public async Task<IResult<bool>> OverrideSplit(PurchasePlanOverrideSplitRequest request)
        {
            var db = unitOfWork.DbContext;
            var pct = (request.Pct ?? []).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (pct.Count > 0 && Math.Abs(pct.Values.Sum() - 100m) > 0.01m)
            {
                return Result<bool>.Fail($"Tổng tỷ lệ chia phải bằng 100% (đang {pct.Values.Sum():0.##}%)", false);
            }
            if (pct.Count > 0 && string.IsNullOrWhiteSpace(request.Reason))
            {
                return Result<bool>.Fail("Nhập lý do sửa phần chia NCC", false);
            }
            db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = SupplierSplit.KeyOf(request.NeedDate.Date, request.IngredientId), EntityName = SupplierSplit.EntityName, Action = pct.Count > 0 ? "Override" : "Reset",
                Changes = JsonConvert.SerializeObject(new SupplierSplit.ManualSplit { Date = request.NeedDate.Date, IngredientId = request.IngredientId, Pct = pct, Reason = request.Reason?.Trim() }),
                CreatedDate = clock.Now, CreatedBy = currentActor.Actor?.UserId, IsDeleted = false,
            });
            await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, pct.Count > 0 ? "Đã lưu phần chia NCC (đề nghị sẽ cần Trưởng mua hàng duyệt)" : "Đã bỏ phần chia sửa tay – về tỷ lệ tính");
        }
    }
}
