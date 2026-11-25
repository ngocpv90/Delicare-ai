using System.IO.Compression;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.9 (D-87): bảng kê tháng mọi khách – tự tính khi mở (không còn "Lập bảng kê"), trạng thái Sẵn sàng / Chờ đối soát / Không phát sinh,
    /// ⚠ lệch tiền suất &gt; <c>BusinessRules:BillingMonthDeviationPct</c> so tháng trước; xuất lô Excel nhiều sheet / zip PDF; nhật ký xuất trong AuditChange (không thêm bảng).
    /// </summary>
    public partial class BillingStatementService
    {
        public const string StatusReady = "READY", StatusPending = "PENDING", StatusNone = "NONE";
        private const string LogEntity = "BillingStatement";
        private readonly ICurrentActor _currentActor = currentActor;

        private (DateTime From, DateTime To) MonthOf(DateTime? any)
        {
            DateTime d = (any ?? _clock.Today.AddMonths(-1)).Date;
            DateTime from = new(d.Year, d.Month, 1);
            return (from, from.AddMonths(1).AddDays(-1));
        }

        /// <summary>Khách có đơn (chưa hủy) trong kỳ hoặc hợp đồng Hiệu lực giao kỳ.</summary>
        private async Task<List<Guid>> MonthCustomersAsync(DateTime from, DateTime to, Guid? branchId)
        {
            var db = _unitOfWork.DbContext;
            DateTime end = to.AddDays(1);
            var withOrders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.Status != OrderStatus.Cancelled && o.MealDate >= from && o.MealDate < end).Select(o => o.CustomerId).Distinct().ToListAsync();
            var withContract = await db.Contract.AsNoTracking().Where(c => !c.IsDeleted && c.Status == ContractStatus.Active && c.EffectiveFrom < end && (c.EffectiveTo == null || c.EffectiveTo >= from)).Select(c => c.CustomerId).Distinct().ToListAsync();
            var ids = withOrders.Union(withContract).ToList();
            return await db.Customer.AsNoTracking().Where(c => ids.Contains(c.Id) && !c.IsDeleted && (branchId == null || c.BranchId == branchId)).Select(c => c.Id).ToListAsync();
        }

        public async Task<IResult<BillingMonthModel>> GetMonthOverview(BillingMonthRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var (from, to) = MonthOf(request.Month);
                var model = new BillingMonthModel { From = from, To = to, DeviationPct = _rules.BillingMonthDeviationPct };
                var ids = await MonthCustomersAsync(from, to, request.BranchId);
                // Tháng trước: Σ giá trị đối soát (chốt / tạm) theo khách – để so lệch.
                DateTime pFrom = from.AddMonths(-1), pEnd = from;
                var prev = await (from r in db.OrderReconciliation.AsNoTracking()
                                  join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                                  where !r.IsDeleted && !o.IsDeleted && o.MealDate >= pFrom && o.MealDate < pEnd && ids.Contains(o.CustomerId)
                                  group r by o.CustomerId into g
                                  select new { CustomerId = g.Key, Amount = g.Sum(x => x.FinalAmount ?? 0m) }).ToDictionaryAsync(x => x.CustomerId, x => x.Amount);
                foreach (var id in ids)
                {
                    var (m, error) = await BuildAsync(new BillingStatementRequest { CustomerId = id, From = from, To = to });
                    if (m == null)
                    {
                        Log.Warning("[BillingStatement] {Customer}: {Error}", id, error);
                        continue;
                    }
                    var row = new BillingMonthCustomerModel
                    {
                        CustomerId = id, CustomerCode = m.CustomerCode, CustomerName = m.CustomerName, ContractNumber = m.ContractNumber,
                        BilledQuantity = m.TotalBilledQuantity, FaultQuantity = m.TotalFaultQuantity, MealAmount = m.MealAmount, ContainerFee = m.ContainerFeeAmount,
                        VatAmount = m.VatAmount, GrandTotal = m.GrandTotal, PendingOrders = m.ListPending.Count, CompensationOrders = m.CompensationOrderCount,
                        PreviousMealAmount = prev.TryGetValue(id, out var p) ? p : null,
                    };
                    row.Status = row.PendingOrders > 0 ? StatusPending : m.ListRow.Count > 0 || row.ContainerFee > 0 ? StatusReady : StatusNone;
                    if (row.PreviousMealAmount is > 0 && row.MealAmount > 0)
                    {
                        row.ChangePct = Math.Round((row.MealAmount - row.PreviousMealAmount.Value) / row.PreviousMealAmount.Value * 100m, 1);
                        row.Warn = Math.Abs(row.ChangePct.Value) > _rules.BillingMonthDeviationPct;
                    }
                    model.ListCustomer.Add(row);
                    if (row.Status != StatusNone)
                    {
                        model.ActiveCount++;
                    }
                    if (row.Status == StatusReady)
                    {
                        model.ReadyCount++;
                    }
                    if (row.PendingOrders > 0)
                    {
                        model.PendingCustomers++;
                        model.PendingOrders += row.PendingOrders;
                        model.ListPendingGroup.Add(new BillingPendingOrderGroupModel { CustomerId = id, CustomerName = m.CustomerName, ListOrder = m.ListPending });
                    }
                    model.GrandTotal += row.GrandTotal;
                    model.MealAmount += row.MealAmount;
                    model.ContainerFee += row.ContainerFee;
                    model.VatAmount += row.VatAmount;
                    model.BilledQuantity += row.BilledQuantity;
                }
                // Có phát sinh trước, không phát sinh xếp cuối; trong nhóm theo tên.
                model.ListCustomer = model.ListCustomer.OrderBy(c => c.Status == StatusNone ? 1 : 0).ThenBy(c => c.CustomerName, StringComparer.CurrentCulture).ToList();
                return Result<BillingMonthModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonthOverview: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Xuất lô: xlsx = 1 tệp, sheet "Tổng hợp" + mỗi khách 1 sheet; pdf = zip, mỗi khách 1 PDF <c>BangKe_&lt;MaKH&gt;_&lt;YYYYMM&gt;</c>.</summary>
        public async Task<IResult<FileContentModel>> ExportBatch(BillingBatchRequest request)
        {
            try
            {
                var (from, to) = MonthOf(request.Month);
                var ids = (request.ListCustomerId ?? []).Distinct().ToList();
                if (ids.Count == 0)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.BillingBatchEmpty);
                }
                bool pdf = string.Equals(request.Format, PrintRenderer.Pdf, StringComparison.OrdinalIgnoreCase);
                var statements = new List<BillingStatementModel>();
                foreach (var id in ids)
                {
                    var (m, _) = await BuildAsync(new BillingStatementRequest { CustomerId = id, From = from, To = to });
                    if (m != null)
                    {
                        statements.Add(m);
                    }
                }
                if (statements.Count == 0)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.BillingBatchEmpty);
                }
                string ym = from.ToString("yyyyMM");
                byte[] content;
                string fileName, contentType;
                if (pdf)
                {
                    using var zipStream = new MemoryStream();
                    using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                    {
                        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var m in statements)
                        {
                            string name = $"BangKe_{Safe(m.CustomerCode)}_{ym}";
                            for (int i = 2; !used.Add(name); i++)
                            {
                                name = $"BangKe_{Safe(m.CustomerCode)}_{ym}_{i}";
                            }
                            var entry = zip.CreateEntry(name + ".pdf");
                            await using var es = entry.Open();
                            byte[] one = PrintRenderer.Render([await DocAsync(m)], PrintRenderer.Pdf);
                            await es.WriteAsync(one);
                        }
                    }
                    content = zipStream.ToArray();
                    fileName = $"BangKe_{ym}_{statements.Count}KH.zip";
                    contentType = "application/zip";
                }
                else
                {
                    var docs = new List<PrintDocumentModel> { await SummaryDocAsync(statements, from, to) };
                    foreach (var m in statements)
                    {
                        docs.Add(await DocAsync(m));
                    }
                    content = PrintRenderer.Render(docs, PrintRenderer.Xlsx);
                    fileName = $"BangKe_{ym}_{statements.Count}KH.xlsx";
                    contentType = PrintRenderer.ContentTypeOf(PrintRenderer.Xlsx);
                }
                await LogExportAsync(statements.Select(s => s.CustomerId).ToList(), pdf ? PrintRenderer.Pdf : PrintRenderer.Xlsx, from, to);
                return Result<FileContentModel>.Success(new FileContentModel { Content = content, ContentType = contentType, FileName = fileName }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportBatch: ", ex.ToString()));
                throw;
            }
        }

        private static string Safe(string? code) => string.Concat((code ?? "KH").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));

        private async Task<PrintDocumentModel> SummaryDocAsync(List<BillingStatementModel> list, DateTime from, DateTime to)
        {
            var company = await PrintCompany.ResolveAsync(_unitOfWork.DbContext); // UI-6.46: tên pháp nhân thay chữ cố định
            var doc = new PrintDocumentModel
            {
                Type = "BILLING_SUMMARY", Id = Guid.Empty, Title = "TỔNG HỢP BẢNG KÊ THANH TOÁN", FormCode = "BK-TH", DocumentNo = $"Tổng hợp {from:MM-yyyy}", DocumentDate = _clock.Today,
                CompanyName = company?.Name ?? "DELICARE", CompanyAddress = company?.Address, CompanyPhone = company?.Phone, CompanyTaxCode = company?.TaxCode,
            };
            doc.ListInfo.Add(new PrintInfoRowModel { Label = "Kỳ", Value = $"{D(from)} – {D(to)}" });
            doc.ListHeader.AddRange(["Mã KH", "Khách hàng", "Suất tính tiền", "Tiền suất ăn", "Phí khay", "VAT", "Tổng thanh toán", "Đơn chờ đối soát"]);
            foreach (var m in list)
            {
                doc.ListRow.Add([m.CustomerCode ?? string.Empty, m.CustomerName ?? string.Empty, N(m.TotalBilledQuantity), N(m.MealAmount), N(m.ContainerFeeAmount), N(m.VatAmount), N(m.GrandTotal), m.ListPending.Count == 0 ? "" : N(m.ListPending.Count)]);
            }
            doc.ListFooter.Add(["", "Tổng", N(list.Sum(m => m.TotalBilledQuantity)), N(list.Sum(m => m.MealAmount)), N(list.Sum(m => m.ContainerFeeAmount)), N(list.Sum(m => m.VatAmount)), N(list.Sum(m => m.GrandTotal)), ""]);
            return doc;
        }

        /// <summary>Nhật ký xuất / in bảng kê – AuditChange (EntityName = BillingStatement, IdChange = khách), ghi thêm.</summary>
        private async Task LogExportAsync(IReadOnlyCollection<Guid> customerIds, string format, DateTime from, DateTime to)
        {
            try
            {
                if (_currentActor.Actor is not { } actor)
                {
                    return;
                }
                var db = _unitOfWork.DbContext;
                string action = format == PrintRenderer.Pdf ? "Print" : "Export";
                foreach (var id in customerIds)
                {
                    db.AuditChange.Add(new AuditChange
                    {
                        Id = Guid.NewGuid(), IdChange = id, EntityName = LogEntity, Action = action, CreatedDate = _clock.Now, CreatedBy = actor.UserId, IsDeleted = false,
                        Changes = $"Kỳ {D(from)} – {D(to)} · {(format == PrintRenderer.Pdf ? "PDF" : "Excel")}" + (customerIds.Count > 1 ? $" · xuất lô {customerIds.Count} khách" : string.Empty),
                    });
                }
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BillingStatement] ghi nhật ký xuất");
            }
        }

        public async Task<IResults<BillingExportLogModel>> GetExportHistory(BillingMonthRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var rows = await (from a in db.AuditChange.AsNoTracking()
                                  where a.EntityName == LogEntity && !a.IsDeleted
                                  orderby a.CreatedDate descending
                                  select new { a.CreatedDate, a.CreatedBy, a.IdChange, a.Action, a.Changes }).Take(300).ToListAsync();
                var userIds = rows.Where(r => r.CreatedBy != null).Select(r => r.CreatedBy!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                var cusIds = rows.Where(r => r.IdChange != null).Select(r => r.IdChange!.Value).Distinct().ToList();
                var cus = await db.Customer.AsNoTracking().Where(c => cusIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                string? month = request.Month is { } mo ? $"{new DateTime(mo.Year, mo.Month, 1):dd/MM/yyyy}" : null;
                var list = rows.Where(r => month == null || (r.Changes ?? string.Empty).Contains(month)).Select(r => new BillingExportLogModel
                {
                    At = r.CreatedDate, ByName = r.CreatedBy is { } u ? users.GetValueOrDefault(u) : null,
                    CustomerName = r.IdChange is { } c ? cus.GetValueOrDefault(c) : null, Action = r.Action == "Print" ? "In PDF" : "Xuất Excel", Detail = r.Changes,
                }).ToList();
                return new Results<BillingExportLogModel> { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully, ListData = list, TotalRecords = list.Count };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetExportHistory: ", ex.ToString()));
                throw;
            }
        }
    }
}
