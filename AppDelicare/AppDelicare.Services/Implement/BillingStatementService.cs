using System.Globalization;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Services.Interface;
using Serilog;

namespace AppDelicare.Services.Interface
{
    /// <summary>BIZ-3.2 (D-77 – A7): bảng kê thanh toán tháng (màn crm-billing).</summary>
    public interface IBillingStatementService
    {
        public Task<IResult<BillingStatementModel>> GetStatement(BillingStatementRequest request);
        /// <summary>Xuất Excel (format xlsx – mặc định) / in PDF (format pdf) – dựng bằng PrintRenderer như phiếu in.</summary>
        public Task<IResult<FileContentModel>> Export(BillingStatementRequest request);
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-3.2 (D-77 – A7): bảng kê thanh toán theo khách × kỳ (mặc định tháng trước), lọc hợp đồng. Chỉ đơn có đối soát <b>Đã chốt</b>:
    /// theo ngày × buổi (loại suất): suất tính tiền (BIZ-1.4, JSON Note của đối soát; bản cũ: suất phục vụ), đơn giá đã cố định lúc chốt (BIZ-1.5), thành tiền
    /// (= tiền chốt của đối soát chia theo tỷ lệ dòng). Suất lỗi Delicare hiện để đối chiếu (đã trừ khỏi suất tính tiền). Đơn bù giá 0 (<c>[DON_BU:</c>) không tính.
    /// Phí thất thoát khay = (khay mất trong kỳ − số giao × ngưỡng %) × <c>ContainerLossFee</c>[loại] (mặc định 0 = chưa tính – PROPOSED).
    /// VAT bán hàng <c>SalesVatPct</c> (8 %). Đơn chưa chốt đối soát liệt kê riêng "chờ đối soát". Không lưu dữ liệu mới.
    /// </summary>
    public class BillingStatementService(IUnitOfWork unitOfWork, IClock clock, IOptions<BusinessRulesOptions> rules) : IBillingStatementService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private static string N(decimal v) => v.ToString("#,##0.##", Vi);
        private static string D(DateTime v) => v.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public async Task<IResult<BillingStatementModel>> GetStatement(BillingStatementRequest request)
        {
            try
            {
                var (model, error) = await BuildAsync(request);
                return error != null ? Result<BillingStatementModel>.Fail(error) : Result<BillingStatementModel>.Success(model!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStatement: ", ex.ToString()));
                throw;
            }
        }

        private async Task<(BillingStatementModel? Model, string? Error)> BuildAsync(BillingStatementRequest request)
        {
            var db = _unitOfWork.DbContext;
            DateTime firstOfMonth = new(_clock.Today.Year, _clock.Today.Month, 1);
            DateTime from = (request.From ?? firstOfMonth.AddMonths(-1)).Date, to = (request.To ?? firstOfMonth.AddDays(-1)).Date;
            if (to < from || (to - from).TotalDays > 92)
            {
                return (null, ConstantHelper.Message.BillingRangeInvalid);
            }
            var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted);
            if (customer == null)
            {
                return (null, ConstantHelper.Message.ContractCustomerNotFound);
            }
            var model = new BillingStatementModel
            {
                CustomerId = customer.Id, CustomerCode = customer.Code, CustomerName = customer.LegalName ?? customer.DisplayName ?? customer.Name, TaxCode = customer.TaxCode,
                Address = customer.Address, From = from, To = to, VatPct = _rules.SalesVatPct, BillingBasisName = DeliveryCode.BillingLabel(_rules.ReconciliationBillingBasis),
            };
            if (request.ContractId is { } cid && cid != Guid.Empty)
            {
                var contract = await db.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid && c.CustomerId == customer.Id && !c.IsDeleted);
                if (contract == null)
                {
                    return (null, ConstantHelper.Message.RecordNotFoundMessage);
                }
                (model.ContractId, model.ContractNumber) = (contract.Id, contract.ContractNumber ?? contract.ContractNo);
            }
            DateTime end = to.AddDays(1);
            var orders = await db.Order.AsNoTracking()
                .Where(o => !o.IsDeleted && o.CustomerId == customer.Id && o.MealDate >= from && o.MealDate < end && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Draft
                    && (model.ContractId == null || o.ContractId == model.ContractId))
                .OrderBy(o => o.MealDate).ThenBy(o => o.OrderNumber).ToListAsync();
            var ids = orders.Select(o => o.Id).ToList();
            var recs = (await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && ids.Contains(r.OrderId)).ToListAsync())
                .GroupBy(r => r.OrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReconciliationDate).First());
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && ids.Contains(l.OrderId) && l.Quantity > 0).ToListAsync();
            var mealIds = lines.Where(l => l.ContractMealId != null).Select(l => l.ContractMealId!.Value).Distinct().ToList();
            var mealNames = await db.ContractMeal.AsNoTracking().Where(m => mealIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.MealName);
            var locationIds = orders.Where(o => o.DeliveryLocationId != null).Select(o => o.DeliveryLocationId!.Value).Distinct().ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name ?? l.Code);

            foreach (var order in orders)
            {
                string no = order.OrderNumber ?? order.OrderNo ?? string.Empty;
                if (order.Note?.Contains("[DON_BU:", StringComparison.Ordinal) == true && (order.TotalAmount ?? 0m) == 0m)
                {
                    model.CompensationOrderCount++;
                    continue;
                }
                var own = lines.Where(l => l.OrderId == order.Id).ToList();
                if (!recs.TryGetValue(order.Id, out var rec) || rec.Status != ReconciliationStatus.Closed)
                {
                    model.ListPending.Add(new BillingPendingOrderModel
                    {
                        OrderId = order.Id, OrderNumber = no, MealDate = order.MealDate.Date, OrderedQuantity = own.Sum(l => l.Quantity), OrderAmount = order.TotalAmount ?? own.Sum(l => l.LineAmount),
                        StatusName = rec == null ? OrderStatus.Label(order.Status) + " – chưa đối soát" : "Đối soát " + ReconciliationStatus.Label(rec.Status),
                    });
                    continue;
                }
                var (billed, fault) = BilledOf(rec);
                decimal ordered = own.Sum(l => l.Quantity);
                decimal finalAmount = rec.FinalAmount ?? 0m;
                foreach (var g in own.GroupBy(l => (l.MealType ?? string.Empty).Trim().ToUpperInvariant()))
                {
                    decimal share = ordered > 0 ? g.Sum(l => l.Quantity) / ordered : 0m;
                    decimal qty = Math.Round(billed * share, 2, MidpointRounding.AwayFromZero);
                    decimal amount = Math.Round(finalAmount * share, 0, MidpointRounding.AwayFromZero);
                    var first = g.First();
                    model.ListRow.Add(new BillingStatementRowModel
                    {
                        MealDate = order.MealDate.Date, MealType = first.MealType, MealName = first.ContractMealId is { } m ? mealNames.GetValueOrDefault(m) : null,
                        OrderNumber = no, LocationName = order.DeliveryLocationId is { } lid ? locations.GetValueOrDefault(lid) : null,
                        BilledQuantity = qty, FaultQuantity = Math.Round(fault * share, 2, MidpointRounding.AwayFromZero),
                        UnitPrice = first.UnitPrice ?? (qty > 0 ? Math.Round(amount / qty, 0) : 0m), Amount = amount,
                    });
                }
            }
            model.TotalBilledQuantity = model.ListRow.Sum(r => r.BilledQuantity);
            model.TotalFaultQuantity = model.ListRow.Sum(r => r.FaultQuantity);
            model.MealAmount = model.ListRow.Sum(r => r.Amount);

            foreach (var b in await PackagingRules.BalanceAsync(db, _rules, customer.Id, null, from, to))
            {
                decimal allowed = Math.Round(b.PeriodOutQuantity * b.LossThresholdPct / 100m, 2);
                decimal over = Math.Max(0m, b.PeriodLossQuantity - allowed);
                if (over <= 0)
                {
                    continue;
                }
                decimal fee = _rules.ContainerLossFee.GetValueOrDefault(b.PackagingType);
                model.ListContainerFee.Add(new BillingContainerFeeModel
                {
                    PackagingType = b.PackagingType, PeriodOutQuantity = b.PeriodOutQuantity, PeriodLossQuantity = b.PeriodLossQuantity, ThresholdPct = b.LossThresholdPct,
                    OverQuantity = over, FeePerUnit = fee, Amount = Math.Round(over * fee, 0),
                });
            }
            model.ContainerFeeAmount = model.ListContainerFee.Sum(f => f.Amount);
            model.SubTotal = model.MealAmount + model.ContainerFeeAmount;
            model.VatAmount = Math.Round(model.SubTotal * model.VatPct / 100m, 0, MidpointRounding.AwayFromZero);
            model.GrandTotal = model.SubTotal + model.VatAmount;
            return (model, null);
        }

        /// <summary>Suất tính tiền + suất lỗi Delicare trong JSON Note của đối soát (BIZ-1.4); bản cũ: suất phục vụ, lỗi 0.</summary>
        private static (decimal Billed, decimal Fault) BilledOf(Model.Entity.OrderReconciliation rec)
        {
            if (!string.IsNullOrWhiteSpace(rec.Note) && rec.Note.TrimStart().StartsWith('{'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rec.Note);
                    var root = doc.RootElement;
                    decimal billed = root.TryGetProperty("billingQuantity", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetDecimal() : rec.AcceptedQuantity;
                    decimal fault = root.TryGetProperty("fault", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetDecimal() : 0m;
                    return (billed, fault);
                }
                catch (JsonException)
                {
                    // Note ghi tay cũ.
                }
            }
            return (rec.AcceptedQuantity, 0m);
        }

        public async Task<IResult<FileContentModel>> Export(BillingStatementRequest request)
        {
            try
            {
                var (m, error) = await BuildAsync(request);
                if (error != null)
                {
                    return Result<FileContentModel>.Fail(error);
                }
                string format = string.Equals(request.Format, PrintRenderer.Pdf, StringComparison.OrdinalIgnoreCase) ? PrintRenderer.Pdf : PrintRenderer.Xlsx;
                var company = await _unitOfWork.DbContext.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                var doc = new PrintDocumentModel
                {
                    Type = "BILLING_STATEMENT", Id = m!.CustomerId, Title = "BẢNG KÊ XÁC NHẬN KHỐI LƯỢNG SUẤT ĂN", FormCode = "BK-TT",
                    DocumentNo = $"BK-{m.CustomerCode}-{m.From:yyMM}", DocumentDate = _clock.Today,
                    CompanyName = company?.Name ?? "DELICARE", CompanyAddress = company?.Address, CompanyPhone = company?.Phone, CompanyTaxCode = company?.TaxCode,
                };
                doc.ListInfo.AddRange([
                    new PrintInfoRowModel { Label = "Khách hàng", Value = m.CustomerName },
                    new PrintInfoRowModel { Label = "Mã số thuế", Value = m.TaxCode },
                    new PrintInfoRowModel { Label = "Địa chỉ", Value = m.Address },
                    new PrintInfoRowModel { Label = "Hợp đồng", Value = m.ContractNumber ?? "Tất cả" },
                    new PrintInfoRowModel { Label = "Kỳ", Value = $"{D(m.From)} – {D(m.To)}" },
                    new PrintInfoRowModel { Label = "Cách tính", Value = m.BillingBasisName },
                ]);
                doc.ListHeader.AddRange(["Ngày", "Buổi", "Đơn", "Điểm giao", "Suất tính tiền", "Suất lỗi (đã trừ)", "Đơn giá", "Thành tiền"]);
                foreach (var r in m.ListRow)
                {
                    doc.ListRow.Add([D(r.MealDate), r.MealName ?? r.MealType ?? string.Empty, r.OrderNumber ?? string.Empty, r.LocationName ?? string.Empty, N(r.BilledQuantity), N(r.FaultQuantity), N(r.UnitPrice), N(r.Amount)]);
                }
                doc.ListFooter.Add(["", "", "", "Cộng tiền suất ăn", N(m.TotalBilledQuantity), N(m.TotalFaultQuantity), "", N(m.MealAmount)]);
                foreach (var f in m.ListContainerFee)
                {
                    doc.ListFooter.Add(["", "", "", $"Phí thất thoát khay {f.PackagingType}", N(f.OverQuantity), "", N(f.FeePerUnit), N(f.Amount)]);
                }
                doc.ListFooter.Add(["", "", "", "Cộng chưa VAT", "", "", "", N(m.SubTotal)]);
                doc.ListFooter.Add(["", "", "", $"VAT {N(m.VatPct)}%", "", "", "", N(m.VatAmount)]);
                doc.ListFooter.Add(["", "", "", "TỔNG THANH TOÁN", "", "", "", N(m.GrandTotal)]);
                if (m.ListPending.Count != 0)
                {
                    doc.ListNote.Add($"Chưa tính: {m.ListPending.Count} đơn chờ đối soát ({string.Join(", ", m.ListPending.Take(15).Select(p => p.OrderNumber))}).");
                }
                if (m.CompensationOrderCount != 0)
                {
                    doc.ListNote.Add($"{m.CompensationOrderCount} đơn bù do lỗi Delicare (giá 0) không tính tiền.");
                }
                doc.ListSignature.AddRange(["Đại diện khách hàng", "Kế toán Delicare", "Đại diện Delicare"]);
                byte[] content = PrintRenderer.Render([doc], format);
                return Result<FileContentModel>.Success(new FileContentModel
                {
                    Content = content, ContentType = PrintRenderer.ContentTypeOf(format), FileName = $"BangKe_{m.CustomerCode}_{m.From:yyyyMMdd}_{m.To:yyyyMMdd}.{format}",
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Export: ", ex.ToString()));
                throw;
            }
        }
    }
}
