#nullable enable
using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// APR-1.2 (D-81 e, f): khuôn tóm tắt chung của từng loại yêu cầu duyệt – handler gọi các method ở đây (một chỗ, dễ rà).
    /// CHỈ ĐỌC dữ liệu có sẵn (không bảng / cột mới). Cảnh báo (≤ 3) theo D-81 (f), ngưỡng trong BusinessRules; không có cảnh báo = Bình thường.
    /// Cảnh báo chung "người gửi bị từ chối loại này ≥ N lần / 30 ngày" do engine thêm (ApprovalRequestService.BuildSummaryAsync).
    /// </summary>
    public static class ApprovalSummaries
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public static string Money(decimal? v) => v == null ? "–" : v.Value.ToString("N0", Vi) + " đ";
        public static string Num(decimal? v) => v == null ? "–" : v.Value.ToString("#,##0.##", Vi);
        public static string Pct(decimal v) => v.ToString("0.#", Vi) + " %";
        public static string Day(DateTime? d) => d == null ? "–" : d.Value.ToString("dd/MM", Vi);
        public static string Date(DateTime? d) => d == null ? "–" : d.Value.ToString("dd/MM/yyyy", Vi);
        private static string Signed(decimal v) => (v > 0 ? "+" : v < 0 ? "−" : string.Empty) + Math.Abs(v).ToString("#,##0.##", Vi);

        /// <summary>Tên loại + phân hệ (lọc nhanh) + màn chứng từ gốc theo BusinessType (+ cấp).</summary>
        public static (string TypeName, string Module, string Screen) TypeOf(string? businessType, int stepNo = 1, int stepCount = 1, string? stepScreen = null)
        {
            string lv = stepCount > 1 ? $" – cấp {stepNo}" : string.Empty;
            return businessType switch
            {
                PurchaseOrderStatus.BusinessType => (stepScreen == PurchaseOrderStatus.DirectorScreenId ? "Đơn mua hàng ≥ 50 triệu" : "Đơn mua hàng", "Mua hàng", "scm-po"),
                PurchaseRequestStatus.BusinessType => ("Đề nghị mua" + lv, "Mua hàng", "scm-pr"),
                OrderStatus.ChangeAfterCutoffBusinessType => ("Sửa đơn sau chốt", "Kinh doanh", "crm-order"),
                OrderStatus.DeviationBusinessType => ("Lệch suất đơn đặt", "Kinh doanh", "crm-order"),
                ContractStatus.BusinessType => ("Hợp đồng", "Kinh doanh", "crm-contract"),
                RecipeStatus.BusinessType => ("Công thức chế biến", "Thực đơn", "menu-recipe"),
                MenuChangeStatus.BusinessType => ("Yêu cầu đổi món", "Thực đơn", "menu-change-request"),
                StockCountStatus.BusinessType => ("Kiểm kê" + lv, "Kho", "scm-stockcount"),
                StockAdjustmentStatus.BusinessType => ("Điều chỉnh tồn", "Kho", "scm-inv-balance"),
                LeaveRequestStatus.BusinessType => ("Đơn xin nghỉ", "Nhân sự", "hr-leave"),
                ShiftSwapStatus.BusinessType => ("Đơn đổi ca", "Nhân sự", "hr-swap"),
                PlanBusinessType => ("Kế hoạch sản xuất", "Sản xuất", "prod-plan"),
                _ => (businessType ?? "Yêu cầu duyệt", "Khác", "wf-inbox"),
            };
        }

        /// <summary>D-81 (g): kế hoạch SX giữ Nháp chờ COO (BIZ-2.4 HELD) – loại ảo trong hộp duyệt, duyệt bằng ProductionPlan/Approve.</summary>
        public const string PlanBusinessType = "PRODUCTION_PLAN_HELD";

        private static void Flag(ApprovalSummaryModel s, string text, string severity = "warn") => s.Flags.Add(new ApprovalFlagModel { Text = text, Severity = severity });

        private static void Lines(ApprovalSummaryModel s, IEnumerable<ApprovalLineModel> lines)
        {
            var all = lines.ToList();
            s.LineCount = all.Count;
            s.Lines = [.. all.OrderByDescending(l => l.Highlight).Take(20)];
        }

        // ================================================================== Mua hàng

        public static async Task<ApprovalSummaryModel?> PurchaseOrderAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var po = await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (po == null)
            {
                return null;
            }
            string? supplier = await db.Supplier.AsNoTracking().Where(s => s.Id == po.SupplierId).Select(s => s.Name).FirstOrDefaultAsync();
            var lines = await (from l in db.PurchaseOrderLine.AsNoTracking()
                               join i in db.Ingredient.AsNoTracking() on l.IngredientId equals i.Id into ii
                               from i in ii.DefaultIfEmpty()
                               join u in db.UnitMeasure.AsNoTracking() on l.UnitId equals u.Id into uu
                               from u in uu.DefaultIfEmpty()
                               where l.PurchaseOrderId == id && !l.IsDeleted
                               select new { l.IngredientId, Name = i != null ? i.Name : null, Unit = u != null ? (u.Symbol ?? u.Code) : null, l.Quantity, l.UnitPrice })
                              .ToListAsync();
            var ingIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            // Giá lần mua trước cùng NCC (PO khác, đã qua Nháp / không hủy) – lần gần nhất.
            var prev = await (from l in db.PurchaseOrderLine.AsNoTracking()
                              join p in db.PurchaseOrder.AsNoTracking() on l.PurchaseOrderId equals p.Id
                              where !l.IsDeleted && !p.IsDeleted && p.Id != id && p.SupplierId == po.SupplierId && ingIds.Contains(l.IngredientId)
                                    && p.Status != PurchaseOrderStatus.Draft && p.Status != PurchaseOrderStatus.Cancelled && p.OrderDate <= po.OrderDate
                              select new { l.IngredientId, l.UnitPrice, p.OrderDate }).ToListAsync();
            var lastPrice = prev.GroupBy(x => x.IngredientId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OrderDate).First().UnitPrice);
            var preferred = await db.SupplierMaterial.AsNoTracking()
                .Where(m => !m.IsDeleted && m.IsPreferred && ingIds.Contains(m.IngredientId) && (m.Status == null || m.Status == "Active"))
                .Select(m => new { m.IngredientId, m.SupplierId }).ToListAsync();
            decimal total = po.TotalAmount ?? lines.Sum(l => l.Quantity * l.UnitPrice);
            bool director = ctx.StepScreenId == PurchaseOrderStatus.DirectorScreenId || total >= ctx.Rules.PurchaseOrderDirectorApprovalAmount;
            var s = new ApprovalSummaryModel
            {
                DocumentNo = po.PurchaseOrderNumber ?? po.PONo,
                Counterparty = supplier,
                MainValue = Money(total),
                Title = $"NCC {supplier ?? "–"} · {Money(total)} · {lines.Count} dòng",
                Reason = director ? $"Đơn mua ≥ {Money(ctx.Rules.PurchaseOrderDirectorApprovalAmount)} – Giám đốc duyệt" : "Đơn mua cần duyệt trước khi gửi nhà cung cấp",
                OpenLink = "scm-po?id=" + id,
                LineHeaders = ["Nguyên liệu", "Số lượng", "Đơn giá"],
            };
            if (po.PurchaseRequestId == null)
            {
                s.Reason += "; lập tay, không từ đề nghị mua đã duyệt";
                Flag(s, "Đơn mua lập tay, không từ đề nghị mua đã duyệt");
            }
            var rows = new List<ApprovalLineModel>();
            foreach (var l in lines)
            {
                string price = Num(l.UnitPrice);
                bool high = false;
                if (lastPrice.TryGetValue(l.IngredientId, out var before) && before > 0)
                {
                    decimal pct = Math.Round((l.UnitPrice - before) / before * 100m, 1);
                    if (pct >= ctx.Rules.ApprovalFlagPricePct)
                    {
                        high = true;
                        price += $" (+{Pct(pct)})";
                        Flag(s, $"Giá {l.Name} cao hơn lần mua trước {Pct(pct)} ({Num(before)} → {Num(l.UnitPrice)})");
                    }
                }
                var pref = preferred.Where(p => p.IngredientId == l.IngredientId).Select(p => p.SupplierId).ToList();
                if (pref.Count > 0 && !pref.Contains(po.SupplierId))
                {
                    high = true;
                    Flag(s, $"Không phải NCC gợi ý cho {l.Name}");
                }
                rows.Add(new ApprovalLineModel { Cells = [l.Name, $"{Num(l.Quantity)} {l.Unit}".Trim(), price], Highlight = high });
            }
            Lines(s, rows);
            s.Info.Add(new() { Label = "Nhà cung cấp", Value = supplier });
            s.Info.Add(new() { Label = "Ngày đặt", Value = Date(po.OrderDate) });
            s.Info.Add(new() { Label = "Ngày giao", Value = Date(po.ExpectedDeliveryDate ?? po.ExpectedDate) });
            s.Info.Add(new() { Label = "Nguồn", Value = po.PurchaseRequestId == null ? "Lập tay" : "Từ đề nghị mua" });
            return s;
        }

        public static async Task<ApprovalSummaryModel?> PurchaseRequestAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var pr = await db.PurchaseRequest.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (pr == null)
            {
                return null;
            }
            var lines = await (from l in db.PurchaseRequestLine.AsNoTracking()
                               join i in db.Ingredient.AsNoTracking() on l.IngredientId equals i.Id into ii
                               from i in ii.DefaultIfEmpty()
                               join u in db.UnitMeasure.AsNoTracking() on l.UnitId equals u.Id into uu
                               from u in uu.DefaultIfEmpty()
                               where l.PurchaseRequestId == id && !l.IsDeleted
                               select new { l.IngredientId, Name = i != null ? i.Name : null, Unit = u != null ? (u.Symbol ?? u.Code) : null, l.Quantity, l.RequiredDate })
                              .ToListAsync();
            var ingIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            // Vượt định mức tồn tối đa: tồn hiện có + đề nghị > Max (tổng các kho có định mức).
            var policy = (await db.InventoryPolicy.AsNoTracking().Where(p => !p.IsDeleted && ingIds.Contains(p.IngredientId))
                    .Select(p => new { p.IngredientId, Max = p.MaximumStock ?? p.MaxStock }).ToListAsync())
                .Where(p => p.Max is > 0).GroupBy(p => p.IngredientId).ToDictionary(g => g.Key, g => g.Sum(x => x.Max!.Value));
            var onHand = (await db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && ingIds.Contains(b.IngredientId))
                    .Select(b => new { b.IngredientId, b.OnHandQuantity }).ToListAsync())
                .GroupBy(b => b.IngredientId).ToDictionary(g => g.Key, g => g.Sum(x => x.OnHandQuantity));
            var s = new ApprovalSummaryModel
            {
                DocumentNo = pr.RequestNumber ?? pr.RequestNo,
                MainValue = $"{lines.Count} nguyên liệu",
                Title = $"{lines.Count} nguyên liệu · cần {Day(pr.RequiredDate ?? lines.Min(l => l.RequiredDate))}",
                Reason = ctx.StepCount > 1 && ctx.StepNo > 1 ? $"Đề nghị mua 2 cấp – cấp 1 đã duyệt, chờ cấp {ctx.StepNo}" : "Đề nghị mua 2 cấp – cấp 1",
                OpenLink = "scm-pr?id=" + id,
                LineHeaders = ["Nguyên liệu", "Số lượng", "Ngày cần"],
            };
            var rows = new List<ApprovalLineModel>();
            foreach (var l in lines)
            {
                bool over = policy.TryGetValue(l.IngredientId, out var max) && onHand.GetValueOrDefault(l.IngredientId) + l.Quantity > max;
                if (over)
                {
                    Flag(s, $"{l.Name}: tồn {Num(onHand.GetValueOrDefault(l.IngredientId))} + đề nghị {Num(l.Quantity)} vượt định mức tối đa {Num(max)}");
                }
                rows.Add(new ApprovalLineModel { Cells = [l.Name, $"{Num(l.Quantity)} {l.Unit}".Trim(), Day(l.RequiredDate ?? pr.RequiredDate)], Highlight = over });
            }
            Lines(s, rows);
            s.Info.Add(new() { Label = "Ngày lập", Value = Date(pr.RequestDate) });
            s.Info.Add(new() { Label = "Ngày cần", Value = Date(pr.RequiredDate) });
            s.Info.Add(new() { Label = "So định mức", Value = s.Flags.Count == 0 ? "Trong định mức" : $"{s.Flags.Count} nguyên liệu vượt định mức tối đa" });
            if (!string.IsNullOrWhiteSpace(pr.Note))
            {
                s.Info.Add(new() { Label = "Ghi chú", Value = pr.Note });
            }
            return s;
        }

        // ================================================================== Kinh doanh

        /// <summary>Đề xuất sửa đơn (sau chốt / lệch suất): Lines [{Id, Quantity}] + NewLines [{ContractMealId, ShiftId, Quantity}] (+ Reason, Kind).</summary>
        public static async Task<ApprovalSummaryModel?> OrderChangeAsync(Guid id, ApprovalSummaryContext ctx, bool afterCutoff)
        {
            var db = ctx.Db;
            var o = await db.Order.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (o == null)
            {
                return null;
            }
            string? customer = await db.Customer.AsNoTracking().Where(c => c.Id == o.CustomerId).Select(c => c.Name).FirstOrDefaultAsync();
            var current = await (from l in db.OrderLine.AsNoTracking()
                                 join m in db.ContractMeal.AsNoTracking() on l.ContractMealId equals m.Id into mm
                                 from m in mm.DefaultIfEmpty()
                                 join sh in db.Shift.AsNoTracking() on l.ShiftId equals sh.Id into ss
                                 from sh in ss.DefaultIfEmpty()
                                 where l.OrderId == id && !l.IsDeleted
                                 select new { l.Id, Meal = m != null ? (m.MealName ?? m.MealType) : l.MealType, Shift = sh != null ? sh.Name : null, l.Quantity, l.UnitPrice })
                                .ToListAsync();
            JObject? p = null;
            JToken? root = null;
            try
            {
                root = string.IsNullOrWhiteSpace(ctx.ProposalJson) ? null : JToken.Parse(ctx.ProposalJson);
                p = root as JObject;
            }
            catch (Newtonsoft.Json.JsonException)
            {
            }
            // Lệch suất cũ: đề xuất là mảng [{Id, Quantity}].
            var changeLines = (p?.GetValue("Lines", StringComparison.OrdinalIgnoreCase) as JArray ?? root as JArray ?? [])
                .Select(t => new { Id = (Guid?)t["Id"] ?? (Guid?)t["id"], Quantity = (decimal?)t["Quantity"] ?? (decimal?)t["quantity"] }).ToList();
            var newLines = (p?.GetValue("NewLines", StringComparison.OrdinalIgnoreCase) as JArray ?? [])
                .Select(t => new { Meal = (Guid?)t["ContractMealId"] ?? (Guid?)t["contractMealId"], Quantity = (decimal?)t["Quantity"] ?? (decimal?)t["quantity"] }).ToList();
            var newMealIds = newLines.Where(n => n.Meal != null).Select(n => n.Meal!.Value).ToList();
            var newMeals = await db.ContractMeal.AsNoTracking().Where(m => newMealIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.MealName ?? m.MealType);
            string kind = (string?)p?.GetValue("Kind", StringComparison.OrdinalIgnoreCase) ?? "CHANGE";
            string? reason = (string?)p?.GetValue("Reason", StringComparison.OrdinalIgnoreCase);
            decimal before = current.Sum(l => l.Quantity);
            decimal after = current.Sum(l => changeLines.FirstOrDefault(c => c.Id == l.Id)?.Quantity ?? l.Quantity) + newLines.Sum(n => n.Quantity ?? 0);
            bool create = kind == "CREATE";
            if (create)
            {
                before = 0;
                after = current.Sum(l => l.Quantity);
            }
            decimal delta = after - before;
            decimal pct = before > 0 ? Math.Round(delta / before * 100m, 1) : 100m;
            var s = new ApprovalSummaryModel
            {
                DocumentNo = o.OrderNumber ?? o.OrderNo,
                Counterparty = customer,
                MainValue = create ? $"{Num(after)} suất" : $"{Num(before)} → {Num(after)} suất",
                Title = create ? $"{customer} · ngày ăn {Day(o.MealDate)}: thêm đơn {Num(after)} suất"
                    : $"{customer} · ngày ăn {Day(o.MealDate)}: {Num(before)} → {Num(after)} suất ({(delta >= 0 ? "+" : "−")}{Pct(Math.Abs(pct))})",
                Reason = afterCutoff ? (create ? "Thêm đơn sau giờ chốt" : "Khách đổi số sau giờ chốt") + (string.IsNullOrWhiteSpace(reason) ? string.Empty : " – " + reason)
                    : "Số suất lệch so với dự kiến – cần duyệt",
                OpenLink = "crm-order?id=" + id,
                LineHeaders = ["Suất / ca", "Trước", "Sau"],
            };
            var rows = new List<ApprovalLineModel>();
            foreach (var l in current)
            {
                var c = changeLines.FirstOrDefault(x => x.Id == l.Id);
                decimal nq = create ? l.Quantity : c?.Quantity ?? l.Quantity;
                decimal oq = create ? 0 : l.Quantity;
                rows.Add(new ApprovalLineModel { Cells = [$"{l.Meal}{(l.Shift != null ? " · " + l.Shift : "")}", Num(oq), Num(nq)], Highlight = nq != oq });
                if (nq != oq)
                {
                    s.Changes.Add(new ApprovalChangeModel { Label = $"{l.Meal}{(l.Shift != null ? " · " + l.Shift : "")}", Before = Num(oq), After = Num(nq) });
                }
            }
            foreach (var n in newLines)
            {
                string name = n.Meal is { } mid && newMeals.TryGetValue(mid, out var nm) ? nm ?? "Dòng mới" : "Dòng mới";
                rows.Add(new ApprovalLineModel { Cells = [name + " (mới)", "0", Num(n.Quantity)], Highlight = true });
                s.Changes.Add(new ApprovalChangeModel { Label = name + " (mới)", Before = "0", After = Num(n.Quantity) });
            }
            Lines(s, rows);
            decimal unit = current.Where(l => l.UnitPrice != null).Select(l => l.UnitPrice!.Value).DefaultIfEmpty(0).Average();
            s.Info.Add(new() { Label = "Khách hàng", Value = customer });
            s.Info.Add(new() { Label = "Ngày ăn", Value = Date(o.MealDate) });
            s.Info.Add(new() { Label = "Thay đổi", Value = $"{Signed(delta)} suất" });
            if (unit > 0)
            {
                s.Info.Add(new() { Label = "Thành tiền thay đổi", Value = (delta >= 0 ? "+" : "−") + Money(Math.Abs(delta * unit)) });
            }
            // D-81 (f): kế hoạch ngày ăn đã phát hành → sẽ sinh mẻ bổ sung (D-78 (1)); lệch > 30 %.
            DateTime day = o.MealDate.Date, next = day.AddDays(1);
            if (afterCutoff && await db.ProductionPlan.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.ProductionDate >= day && x.ProductionDate < next
                    && (x.Status == ProductionPlanStatus.Approved || x.Status == ProductionPlanStatus.Released || x.Status == ProductionPlanStatus.Completed)))
            {
                Flag(s, $"Kế hoạch SX {Day(day)} đã duyệt / phát hành – duyệt sẽ sinh lệnh + mẻ bổ sung");
            }
            if (!create && Math.Abs(pct) > ctx.Rules.PortionDeviationPctNeedApproval)
            {
                Flag(s, $"Lệch {Pct(Math.Abs(pct))} (> {Pct(ctx.Rules.PortionDeviationPctNeedApproval)})", "high");
            }
            return s;
        }

        public static async Task<ApprovalSummaryModel?> ContractAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var c = await db.Contract.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (c == null)
            {
                return null;
            }
            string? customer = await db.Customer.AsNoTracking().Where(x => x.Id == c.CustomerId).Select(x => x.Name).FirstOrDefaultAsync();
            var meals = await db.ContractMeal.AsNoTracking().Where(m => m.ContractId == id && !m.IsDeleted).ToListAsync();
            // Giá vốn chuẩn tham chiếu = bình quân giá vốn / suất các giá thành chuẩn đang hiệu lực (PROPOSED – hợp đồng không gắn món).
            var costs = await db.StandardCost.AsNoTracking().Where(x => !x.IsDeleted && (x.Status == null || x.Status == "Active") && x.CostPerServing > 0
                    && (x.EffectiveTo == null || x.EffectiveTo >= ctx.Now.Date)).Select(x => x.CostPerServing).ToListAsync();
            decimal? avgCost = costs.Count > 0 ? Math.Round(costs.Average(), 0) : null;
            var s = new ApprovalSummaryModel
            {
                DocumentNo = c.ContractNumber ?? c.ContractNo,
                Counterparty = customer,
                MainValue = c.ContractValue is > 0 ? Money(c.ContractValue) : $"{meals.Count} loại suất",
                Title = $"{customer} · {meals.Count} loại suất · {Date(c.EffectiveFrom)} – {Date(c.EffectiveTo ?? (DateTime?)c.EndDate)}",
                Reason = "Hợp đồng mới / sửa cần Giám đốc duyệt trước khi hiệu lực",
                OpenLink = "crm-contract?id=" + id,
                LineHeaders = ["Loại suất", "Đơn giá", "So giá vốn chuẩn"],
            };
            var rows = new List<ApprovalLineModel>();
            foreach (var m in meals)
            {
                bool low = avgCost != null && m.UnitPrice != null && m.UnitPrice < avgCost;
                if (low)
                {
                    Flag(s, $"Giá suất {m.MealName ?? m.MealType} {Money(m.UnitPrice)} thấp hơn giá vốn chuẩn bình quân {Money(avgCost)}", "high");
                }
                rows.Add(new ApprovalLineModel { Cells = [m.MealName ?? m.MealType, Money(m.UnitPrice), avgCost == null || m.UnitPrice == null ? "–" : Signed(m.UnitPrice.Value - avgCost.Value)], Highlight = low });
            }
            Lines(s, rows);
            s.Info.Add(new() { Label = "Khách hàng", Value = customer });
            s.Info.Add(new() { Label = "Hiệu lực", Value = $"{Date(c.EffectiveFrom)} – {Date(c.EffectiveTo ?? (DateTime?)c.EndDate)}" });
            s.Info.Add(new() { Label = "Thanh toán", Value = c.PaymentTerm });
            if (avgCost != null)
            {
                s.Info.Add(new() { Label = "Giá vốn chuẩn BQ", Value = Money(avgCost) + " / suất" });
            }
            return s;
        }

        // ================================================================== Thực đơn

        public static async Task<ApprovalSummaryModel?> RecipeAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var r = await db.Recipe.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            string? dish = await db.Dish.AsNoTracking().Where(d => d.Id == r.DishId).Select(d => d.Name).FirstOrDefaultAsync();
            async Task<List<(Guid Ing, string? Name, string? Unit, decimal Qty)>> LinesOf(Guid recipeId)
                => [.. (await (from l in db.RecipeLine.AsNoTracking()
                               join i in db.Ingredient.AsNoTracking() on l.IngredientId equals i.Id into ii
                               from i in ii.DefaultIfEmpty()
                               join u in db.UnitMeasure.AsNoTracking() on l.UnitId equals u.Id into uu
                               from u in uu.DefaultIfEmpty()
                               where l.RecipeId == recipeId && !l.IsDeleted
                               orderby l.SortOrder
                               select new { l.IngredientId, Name = i != null ? i.Name : null, Unit = u != null ? (u.Symbol ?? u.Code) : null, l.Quantity }).ToListAsync())
                    .Select(x => (x.IngredientId, x.Name, x.Unit, x.Quantity))];
            var now = await LinesOf(id);
            // Bản đang áp dụng (Đã khóa) của cùng món → trước → sau theo nguyên liệu.
            var old = await db.Recipe.AsNoTracking().Where(x => x.DishId == r.DishId && x.Id != id && !x.IsDeleted && x.Status == RecipeStatus.Locked)
                .OrderByDescending(x => x.LockedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
            var before = old is { } oid ? (await LinesOf(oid)).GroupBy(x => x.Ing).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty)) : [];
            var s = new ApprovalSummaryModel
            {
                DocumentNo = r.VersionCode,
                Counterparty = dish,
                MainValue = $"{now.Count} nguyên liệu",
                Title = $"{dish} · phiên bản {r.VersionCode} · {now.Count} nguyên liệu",
                Reason = old == null ? "Công thức mới – duyệt xong khóa & áp dụng" : "Phiên bản mới thay bản đang áp dụng – duyệt xong khóa & áp dụng",
                OpenLink = "menu-recipe?id=" + id,
                LineHeaders = ["Nguyên liệu", old == null ? "Định lượng" : "Trước", old == null ? "Đơn vị" : "Sau"],
            };
            var rows = new List<ApprovalLineModel>();
            foreach (var l in now)
            {
                if (old == null)
                {
                    rows.Add(new ApprovalLineModel { Cells = [l.Name, Num(l.Qty), l.Unit] });
                    continue;
                }
                bool has = before.TryGetValue(l.Ing, out var b);
                bool changed = !has || b != l.Qty;
                rows.Add(new ApprovalLineModel { Cells = [l.Name, has ? Num(b) : "(mới)", $"{Num(l.Qty)} {l.Unit}".Trim()], Highlight = changed });
                if (changed)
                {
                    s.Changes.Add(new ApprovalChangeModel { Label = l.Name ?? "–", Before = has ? Num(b) : "–", After = Num(l.Qty) });
                }
            }
            foreach (var gone in before.Keys.Where(k => now.All(n => n.Ing != k)))
            {
                string? name = await db.Ingredient.AsNoTracking().Where(i => i.Id == gone).Select(i => i.Name).FirstOrDefaultAsync();
                rows.Add(new ApprovalLineModel { Cells = [name + " (bỏ)", Num(before[gone]), "0"], Highlight = true });
                s.Changes.Add(new ApprovalChangeModel { Label = name ?? "–", Before = Num(before[gone]), After = "bỏ" });
            }
            Lines(s, rows);
            s.Info.Add(new() { Label = "Món", Value = dish });
            s.Info.Add(new() { Label = "Hiệu lực từ", Value = Date(r.EffectiveFrom) });
            s.Info.Add(new() { Label = "Suất chuẩn", Value = Num(r.StandardOutputQty) });
            if (old != null)
            {
                s.Info.Add(new() { Label = "Thay đổi", Value = $"{s.Changes.Count} nguyên liệu" });
            }
            return s;
        }

        public static async Task<ApprovalSummaryModel?> MenuChangeAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var m = await db.MenuChangeRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (m == null)
            {
                return null;
            }
            var ids = new[] { m.OriginalDishId, m.RequestedDishId }.Where(x => x != null).Select(x => x!.Value).ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            string? dishFrom = m.OriginalDishId is { } a ? dishes.GetValueOrDefault(a) : null, dishTo = m.RequestedDishId is { } b ? dishes.GetValueOrDefault(b) : null;
            string? customer = m.OrderId is { } oid
                ? await (from o in db.Order.AsNoTracking() join c in db.Customer.AsNoTracking() on o.CustomerId equals c.Id where o.Id == oid select c.Name).FirstOrDefaultAsync()
                : null;
            var s = new ApprovalSummaryModel
            {
                DocumentNo = m.RequestNo,
                Counterparty = customer,
                MainValue = m.Quantity is > 0 ? $"{Num(m.Quantity)} suất" : null,
                Title = $"{dishFrom ?? "–"} → {dishTo ?? "–"} · {Day(m.MealDate)}{(customer != null ? " · " + customer : "")}",
                Reason = string.IsNullOrWhiteSpace(m.Reason) ? "Đổi món so với thực đơn đã công bố" : m.Reason,
                OpenLink = "menu-change-request?id=" + id,
            };
            s.Changes.Add(new ApprovalChangeModel { Label = "Món", Before = dishFrom, After = dishTo });
            s.Info.Add(new() { Label = "Ngày ăn", Value = Date(m.MealDate) });
            if (customer != null)
            {
                s.Info.Add(new() { Label = "Khách hàng", Value = customer });
            }
            if (m.Quantity is > 0)
            {
                s.Info.Add(new() { Label = "Số suất", Value = Num(m.Quantity) });
            }
            return s;
        }

        // ================================================================== Kho

        public static async Task<ApprovalSummaryModel?> StockCountAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var sc = await db.StockCount.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (sc == null)
            {
                return null;
            }
            string? wh = await db.Warehouse.AsNoTracking().Where(w => w.Id == sc.WarehouseId).Select(w => w.Name).FirstOrDefaultAsync();
            var lines = await (from l in db.StockCountLine.AsNoTracking()
                               join i in db.Ingredient.AsNoTracking() on l.IngredientId equals i.Id into ii
                               from i in ii.DefaultIfEmpty()
                               join u in db.UnitMeasure.AsNoTracking() on l.UnitId equals u.Id into uu
                               from u in uu.DefaultIfEmpty()
                               where l.StockCountId == id && !l.IsDeleted
                               select new { Name = i != null ? i.Name : null, Unit = u != null ? (u.Symbol ?? u.Code) : null, l.SystemQuantity, l.SystemQty, l.PhysicalQuantity, l.PhysicalQty, l.VarianceValue })
                              .ToListAsync();
            var rows = lines.Select(l =>
            {
                decimal sys = l.SystemQuantity != 0 ? l.SystemQuantity : l.SystemQty ?? 0, phy = l.PhysicalQuantity != 0 || l.PhysicalQty == null ? l.PhysicalQuantity : l.PhysicalQty.Value;
                decimal pct = sys != 0 ? Math.Round((phy - sys) / sys * 100m, 1) : phy != 0 ? 100m : 0m;
                return (l.Name, l.Unit, sys, phy, pct, l.VarianceValue);
            }).ToList();
            var s = new ApprovalSummaryModel
            {
                DocumentNo = sc.CountNumber ?? sc.CountNo,
                Counterparty = wh,
                Title = $"{wh} · {rows.Count(r => r.phy != r.sys)} dòng lệch / {rows.Count} dòng",
                Reason = $"Có dòng lệch > {Pct(ctx.Rules.StockCountVariancePctNeedApproval)} – duyệt trước khi ghi sổ" + (ctx.StepCount > 1 ? $" (cấp {ctx.StepNo}/{ctx.StepCount})" : ""),
                OpenLink = "scm-stockcount?id=" + id,
                LineHeaders = ["Nguyên liệu", "Hệ thống → thực", "Lệch"],
            };
            decimal value = rows.Sum(r => r.VarianceValue ?? 0);
            if (value != 0)
            {
                s.MainValue = (value > 0 ? "+" : "−") + Money(Math.Abs(value));
            }
            foreach (var r in rows.Where(r => Math.Abs(r.pct) > ctx.Rules.ApprovalFlagStockCountLinePct).OrderByDescending(r => Math.Abs(r.pct)).Take(3))
            {
                Flag(s, $"{r.Name} lệch {Signed(r.phy - r.sys)} {r.Unit} ({(r.pct > 0 ? "+" : "−")}{Pct(Math.Abs(r.pct))})");
            }
            Lines(s, rows.Where(r => r.phy != r.sys).OrderByDescending(r => Math.Abs(r.pct))
                .Select(r => new ApprovalLineModel { Cells = [r.Name, $"{Num(r.sys)} → {Num(r.phy)} {r.Unit}".Trim(), (r.pct > 0 ? "+" : r.pct < 0 ? "−" : "") + Pct(Math.Abs(r.pct))], Highlight = Math.Abs(r.pct) > ctx.Rules.ApprovalFlagStockCountLinePct }));
            s.Info.Add(new() { Label = "Kho", Value = wh });
            s.Info.Add(new() { Label = "Ngày đếm", Value = Date(sc.CountDate) });
            s.Info.Add(new() { Label = "Giá trị lệch", Value = s.MainValue ?? "0 đ" });
            if (ctx.StepCount > 1 && ctx.StepNo < ctx.StepCount)
            {
                s.Info.Add(new() { Label = "Cấp sau", Value = "Kế toán" });
            }
            return s;
        }

        public static async Task<ApprovalSummaryModel?> StockAdjustmentAsync(Guid id, ApprovalSummaryContext ctx, string? title)
        {
            var s = new ApprovalSummaryModel { Title = title, Reason = "Điều chỉnh tồn tay phải duyệt trước khi ghi sổ", OpenLink = "scm-inv-balance" };
            JObject? p = null;
            try
            {
                p = string.IsNullOrWhiteSpace(ctx.ProposalJson) ? null : JObject.Parse(ctx.ProposalJson);
            }
            catch (Newtonsoft.Json.JsonException)
            {
            }
            if (p == null)
            {
                return s;
            }
            var db = ctx.Db;
            Guid? ing = (Guid?)p.GetValue("IngredientId", StringComparison.OrdinalIgnoreCase), wh = (Guid?)p.GetValue("WarehouseId", StringComparison.OrdinalIgnoreCase);
            decimal qty = (decimal?)p.GetValue("Quantity", StringComparison.OrdinalIgnoreCase) ?? 0;
            decimal? cost = (decimal?)p.GetValue("UnitCost", StringComparison.OrdinalIgnoreCase);
            string? name = ing is { } i ? await db.Ingredient.AsNoTracking().Where(x => x.Id == i).Select(x => x.Name).FirstOrDefaultAsync() : null;
            string? whName = wh is { } w ? await db.Warehouse.AsNoTracking().Where(x => x.Id == w).Select(x => x.Name).FirstOrDefaultAsync() : null;
            s.Counterparty = whName;
            s.MainValue = cost is > 0 ? (qty >= 0 ? "+" : "−") + Money(Math.Abs(qty * cost.Value)) : $"{Signed(qty)}";
            s.Title ??= $"{name} · {Signed(qty)} · {whName}";
            s.Info.Add(new() { Label = "Nguyên liệu", Value = name });
            s.Info.Add(new() { Label = "Kho", Value = whName });
            s.Info.Add(new() { Label = "Số lượng điều chỉnh", Value = Signed(qty) });
            if ((string?)p.GetValue("Reason", StringComparison.OrdinalIgnoreCase) is { Length: > 0 } reason)
            {
                s.Reason = reason;
            }
            return s;
        }

        // ================================================================== Nhân sự

        public static async Task<ApprovalSummaryModel?> LeaveAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var r = await db.LeaveRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            var emp = await db.Employee.AsNoTracking().Where(e => e.Id == r.EmployeeId).Select(e => new { e.FullName, e.OrganizationUnitId }).FirstOrDefaultAsync();
            string? unit = emp?.OrganizationUnitId is { } ou ? await db.OrganizationUnit.AsNoTracking().Where(o => o.Id == ou).Select(o => o.Name).FirstOrDefaultAsync() : null;
            DateTime dFrom = r.FromDate.Date, dTo = r.ToDate.Date.AddDays(1);
            int days = (int)(r.ToDate.Date - r.FromDate.Date).TotalDays + 1;
            var shifts = await (from a in db.ShiftAssignment.AsNoTracking()
                                join sh in db.Shift.AsNoTracking() on a.ShiftId equals sh.Id into ss
                                from sh in ss.DefaultIfEmpty()
                                where a.EmployeeId == r.EmployeeId && !a.IsDeleted && a.WorkDate >= dFrom && a.WorkDate < dTo && a.AssignmentStatus != "Cancelled"
                                orderby a.WorkDate
                                select new { a.WorkDate, Shift = sh != null ? sh.Name : null }).ToListAsync();
            int usedYear = (await db.LeaveRequest.AsNoTracking().Where(x => x.EmployeeId == r.EmployeeId && x.Id != id && !x.IsDeleted && x.RequestStatus == LeaveRequestStatus.Approved
                    && x.FromDate.Year == r.FromDate.Year).Select(x => new { x.FromDate, x.ToDate }).ToListAsync())
                .Sum(x => (int)(x.ToDate.Date - x.FromDate.Date).TotalDays + 1);
            var s = new ApprovalSummaryModel
            {
                DocumentNo = r.RequestNo,
                Counterparty = emp?.FullName,
                MainValue = $"{days} ngày",
                Title = $"{emp?.FullName}{(unit != null ? " – " + unit : "")} · {Day(r.FromDate)}{(days > 1 ? "–" + Day(r.ToDate) : "")} ({days} ngày)",
                Reason = string.IsNullOrWhiteSpace(r.Reason) ? "Đơn xin nghỉ" : r.Reason,
                OpenLink = "hr-leave?id=" + id,
                LineHeaders = ["Ngày", "Ca", "Người thay"],
            };
            // Đã phân ca trong ngày nghỉ: duyệt sẽ gỡ ca → ca chưa có người thay (D-81 f). Vượt phép năm: GAP-HR-06 (chưa có chỗ lưu số ngày phép).
            Lines(s, shifts.Select(a => new ApprovalLineModel { Cells = [Day(a.WorkDate), a.Shift, "Chưa có"], Highlight = true }));
            if (shifts.Count > 0)
            {
                Flag(s, $"{shifts.Count} ca đã phân trong ngày nghỉ chưa có người thay");
            }
            s.Info.Add(new() { Label = "Loại nghỉ", Value = r.LeaveTypeCode });
            s.Info.Add(new() { Label = "Đã nghỉ năm nay", Value = $"{usedYear} ngày" });
            s.Info.Add(new() { Label = "Ca bị ảnh hưởng", Value = shifts.Count == 0 ? "Không có ca đã phân" : $"{shifts.Count} ca" });
            if (unit != null)
            {
                s.Info.Add(new() { Label = "Đơn vị", Value = unit });
            }
            return s;
        }

        public static async Task<ApprovalSummaryModel?> ShiftSwapAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = ctx.Db;
            var r = await db.ShiftSwapRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return null;
            }
            var empIds = new[] { r.RequesterEmployeeId, r.TargetEmployeeId };
            var names = await db.Employee.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName);
            var asgIds = new List<Guid> { r.CurrentShiftAssignmentId };
            if (r.ProposedShiftAssignmentId is { } pid)
            {
                asgIds.Add(pid);
            }
            var asg = await (from a in db.ShiftAssignment.AsNoTracking()
                             join sh in db.Shift.AsNoTracking() on a.ShiftId equals sh.Id into ss
                             from sh in ss.DefaultIfEmpty()
                             where asgIds.Contains(a.Id)
                             select new { a.Id, a.WorkDate, Shift = sh != null ? sh.Name : null }).ToDictionaryAsync(a => a.Id, a => $"{a.Shift} {Day(a.WorkDate)}");
            string a1 = names.GetValueOrDefault(r.RequesterEmployeeId) ?? "–", a2 = names.GetValueOrDefault(r.TargetEmployeeId) ?? "–";
            string cur = asg.GetValueOrDefault(r.CurrentShiftAssignmentId) ?? "–", prop = r.ProposedShiftAssignmentId is { } p2 ? asg.GetValueOrDefault(p2) ?? "–" : "(nhận ca, không đổi lại)";
            var s = new ApprovalSummaryModel
            {
                DocumentNo = r.RequestNo,
                Counterparty = a1,
                Title = r.ProposedShiftAssignmentId == null ? $"{a1} → {a2} · ca {cur}" : $"{a1} ↔ {a2} · {cur} / {prop}",
                Reason = string.IsNullOrWhiteSpace(r.Reason) ? "Đổi ca giữa 2 nhân viên" : r.Reason,
                OpenLink = "hr-swap?id=" + id,
                LineHeaders = ["Nhân viên", "Trước", "Sau"],
            };
            Lines(s,
            [
                new ApprovalLineModel { Cells = [a1, cur, r.ProposedShiftAssignmentId == null ? "–" : prop] },
                new ApprovalLineModel { Cells = [a2, r.ProposedShiftAssignmentId == null ? "–" : prop, cur] },
            ]);
            s.Info.Add(new() { Label = "Người xin đổi", Value = a1 });
            s.Info.Add(new() { Label = "Người nhận", Value = a2 });
            return s;
        }

        // ================================================================== Sản xuất (D-81 g – ngoài engine)

        public static async Task<ApprovalSummaryModel> HeldPlanAsync(Model.Entity.ProductionPlan plan, DatabaseContext db, string? autoRun)
        {
            var lines = await (from l in db.ProductionPlanLine.AsNoTracking()
                               join sh in db.Shift.AsNoTracking() on l.ShiftId equals sh.Id into ss
                               from sh in ss.DefaultIfEmpty()
                               where l.ProductionPlanId == plan.Id && !l.IsDeleted
                               select new { Shift = sh != null ? sh.Name : null, Qty = l.PlannedQuantity != 0 ? l.PlannedQuantity : l.PlannedQty, l.DishId }).ToListAsync();
            string reason = autoRun ?? string.Empty;
            int cut = reason.IndexOf("chờ COO duyệt:", StringComparison.Ordinal);
            string why = cut >= 0 ? reason[(cut + "chờ COO duyệt:".Length)..].Trim() : string.Empty;
            var s = new ApprovalSummaryModel
            {
                DocumentNo = plan.PlanNumber ?? plan.PlanNo,
                MainValue = $"{Num(lines.Sum(l => l.Qty))} phần món",
                Title = $"Ngày {Day(plan.ProductionDate)} · {Num(lines.Sum(l => l.Qty))} phần món · {lines.Select(l => l.DishId).Distinct().Count()} món",
                Reason = "Hệ thống giữ Nháp sau chốt đơn – không tự phát hành",
                OpenLink = "prod-plan?id=" + plan.Id,
                LineHeaders = ["Ca", "Phần món", "Số món"],
            };
            foreach (var w in why.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(3))
            {
                Flag(s, w);
            }
            Lines(s, lines.GroupBy(l => l.Shift ?? "Cả ngày").Select(g => new ApprovalLineModel { Cells = [g.Key, Num(g.Sum(x => x.Qty)), g.Select(x => x.DishId).Distinct().Count().ToString(Vi)] }));
            s.Info.Add(new() { Label = "Ngày SX", Value = Date(plan.ProductionDate) });
            s.Info.Add(new() { Label = "Trạng thái", Value = ProductionPlanStatus.Label(plan.Status) });
            return s;
        }
    }
}
