using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.4 (D-83, anh chốt 2026-11-23): "Phụ lục điều chỉnh giá" trong hợp đồng – thay mục menu Chính sách giá.
    /// Dữ liệu: bảng <c>PricePolicy</c> sẵn có, <c>ContractId</c> bắt buộc, <c>CustomerId</c> = khách của hợp đồng (không thêm bảng / cột).
    /// Luật: hợp đồng đang Hiệu lực; giá mới &gt; 0; áp dụng từ ≥ ngày mai và trong hiệu lực hợp đồng; đến ngày trống = hết hợp đồng;
    /// không chồng thời gian với phụ lục khác cùng loại suất (Nháp / Chờ duyệt / Hiệu lực); **gửi duyệt bắt buộc có file phụ lục đã ký**
    /// (Tài liệu đính kèm, tài nguyên <c>PricePolicy</c>). Duyệt qua engine (<see cref="PriceAdjustmentStatus.BusinessType"/>); giá đơn cố định lúc chốt (BIZ-1.5).
    /// </summary>
    public class PriceAdjustmentService(IUnitOfWork unitOfWork, IClock clock, IApprovalRequestService approvals, IDocumentNumberService documentNumbers) : IPriceAdjustmentService
    {
        private static readonly string[] OpenStatuses = [PriceAdjustmentStatus.Draft, PriceAdjustmentStatus.PendingApproval, PriceAdjustmentStatus.Active];

        private static string Money(decimal v) => v.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
        private static string Day(DateTime? d) => d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        private static decimal? Pct(decimal oldPrice, decimal newPrice) => oldPrice > 0 ? Math.Round((newPrice - oldPrice) * 100m / oldPrice, 1) : null;

        /// <summary>Số file phụ lục đã đính kèm cho từng dòng (khóa lưu trữ "PricePolicy/{id}/…").</summary>
        private async Task<Dictionary<Guid, int>> FileCountsAsync(IEnumerable<Guid> ids)
        {
            var prefixes = ids.ToDictionary(id => DocumentStorageRules.OwnerPrefix(PriceAdjustmentStatus.Resource, id), id => id);
            if (prefixes.Count == 0)
            {
                return [];
            }
            var keys = await unitOfWork.DbContext.DocumentFile.AsNoTracking()
                .Where(f => !f.IsDeleted && f.FileNameHas != null && f.FileNameHas.StartsWith(PriceAdjustmentStatus.Resource + "/"))
                .Select(f => f.FileNameHas!).ToListAsync();
            var result = new Dictionary<Guid, int>();
            foreach (string k in keys)
            {
                var hit = prefixes.FirstOrDefault(p => k.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase));
                if (hit.Key != null)
                {
                    result[hit.Value] = result.GetValueOrDefault(hit.Value) + 1;
                }
            }
            return result;
        }

        /// <summary>Giá gốc của loại suất trong hợp đồng (ContractMeal) – 0 nếu không có.</summary>
        private static decimal BaseOf(IEnumerable<ContractMeal> meals, string? mealType)
            => meals.FirstOrDefault(m => string.Equals(m.MealType, mealType, StringComparison.OrdinalIgnoreCase))?.UnitPrice ?? 0m;

        public async Task<IResult<ContractPricesModel>> GetContractPrices(Guid contractId)
        {
            try
            {
                var db = unitOfWork.DbContext;
                var contract = await db.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId && !c.IsDeleted);
                if (contract == null)
                {
                    return Result<ContractPricesModel>.Fail(ConstantHelper.Message.ContractNotFound);
                }
                DateTime today = clock.Today;
                var meals = await db.ContractMeal.AsNoTracking().Where(m => m.ContractId == contractId && !m.IsDeleted).OrderBy(m => m.MealType).ToListAsync();
                var rows = await db.PricePolicy.AsNoTracking().Where(p => p.ContractId == contractId && !p.IsDeleted)
                    .OrderByDescending(p => p.EffectiveFrom).ThenBy(p => p.MealType).ToListAsync();
                var files = await FileCountsAsync(rows.Select(r => r.Id));
                var ids = rows.Select(r => (Guid?)r.Id).ToList();
                var requests = await db.ApprovalRequest.AsNoTracking()
                    .Where(a => a.BusinessType == PriceAdjustmentStatus.BusinessType && ids.Contains(a.BusinessId) && !a.IsDeleted)
                    .OrderByDescending(a => a.RequestedAt).ToListAsync();
                var reqIds = requests.Select(r => r.Id).ToList();
                var approvals = await (from d in db.ApprovalDecision.AsNoTracking()
                                       join u in db.User.AsNoTracking() on d.DecidedBy equals u.Id
                                       join p in db.Person.AsNoTracking() on u.PersonId equals p.Id into pp
                                       from p in pp.DefaultIfEmpty()
                                       where reqIds.Contains(d.ApprovalRequestId) && d.DecisionCode == "APPROVE"
                                       select new { d.ApprovalRequestId, d.DecidedAt, Name = p != null && p.FullName != null ? p.FullName : u.UserName }).ToListAsync();
                bool Applies(PricePolicy p) => p.Status == PriceAdjustmentStatus.Active && (p.EffectiveFrom == null || p.EffectiveFrom.Value.Date <= today)
                    && (p.EffectiveTo == null || p.EffectiveTo.Value.Date >= today);
                var model = new ContractPricesModel
                {
                    ContractId = contractId,
                    ContractStatus = contract.Status,
                    CanAdjust = contract.Status == ContractStatus.Active,
                    MinEffectiveFrom = today.AddDays(1),
                    ContractEffectiveTo = contract.EffectiveTo,
                };
                foreach (var m in meals)
                {
                    var now = rows.Where(r => string.Equals(r.MealType, m.MealType, StringComparison.OrdinalIgnoreCase) && Applies(r)).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();
                    model.ListMeal.Add(new ContractPriceRowModel
                    {
                        MealType = m.MealType, MealName = m.MealName, BasePrice = m.UnitPrice ?? 0m,
                        TodayPrice = now?.UnitPrice ?? m.UnitPrice ?? 0m, TodaySource = now == null ? "Giá gốc" : "Phụ lục " + (now.Code ?? string.Empty),
                    });
                }
                foreach (var r in rows)
                {
                    decimal old = BaseOf(meals, r.MealType);
                    var prior = rows.Where(x => x.Id != r.Id && string.Equals(x.MealType, r.MealType, StringComparison.OrdinalIgnoreCase) && x.Status == PriceAdjustmentStatus.Active
                        && x.EffectiveFrom < r.EffectiveFrom).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
                    if (prior != null)
                    {
                        old = prior.UnitPrice;
                    }
                    var req = requests.FirstOrDefault(a => a.BusinessId == r.Id);
                    var ap = req == null ? null : approvals.Where(a => a.ApprovalRequestId == req.Id).OrderByDescending(a => a.DecidedAt).FirstOrDefault();
                    string status = r.Status == PriceAdjustmentStatus.Active && r.EffectiveTo != null && r.EffectiveTo.Value.Date < today ? PriceAdjustmentStatus.Expired : r.Status ?? PriceAdjustmentStatus.Active;
                    model.ListAdjustment.Add(new PriceAdjustmentRowModel
                    {
                        Id = r.Id, Code = r.Code, MealType = r.MealType, OldPrice = old, NewPrice = r.UnitPrice, ChangePct = Pct(old, r.UnitPrice),
                        EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, Status = status, StatusName = PriceAdjustmentStatus.Label(status), Reason = r.Name,
                        FileCount = files.GetValueOrDefault(r.Id), ApprovedBy = ap?.Name, ApprovedAt = ap?.DecidedAt, AppliesToday = Applies(r), ApprovalRequestId = req?.Id,
                    });
                }
                if (!model.CanAdjust)
                {
                    model.ListWarning.Add("Chỉ điều chỉnh giá khi hợp đồng đang Hiệu lực.");
                }
                return Result<ContractPricesModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetContractPrices: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<List<Guid>>> Create(PriceAdjustmentCreateRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var db = unitOfWork.DbContext;
                var contract = await db.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.ContractId && !c.IsDeleted);
                if (contract == null)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(ConstantHelper.Message.ContractNotFound);
                }
                if (contract.Status != ContractStatus.Active)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(ConstantHelper.Message.PriceAdjustContractNotActive);
                }
                var lines = (request.ListLine ?? []).Where(l => !string.IsNullOrWhiteSpace(l.MealType)).ToList();
                if (lines.Count == 0)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(ConstantHelper.Message.PriceAdjustNoLine);
                }
                if (lines.Any(l => l.UnitPrice <= 0))
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(ConstantHelper.Message.PriceAdjustPricePositive);
                }
                if (lines.GroupBy(l => l.MealType!.Trim().ToUpperInvariant()).Any(g => g.Count() > 1))
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(ConstantHelper.Message.PriceAdjustDuplicateMeal);
                }
                DateTime from = request.EffectiveFrom.Date, tomorrow = clock.Today.AddDays(1);
                DateTime? to = request.EffectiveTo?.Date ?? contract.EffectiveTo?.Date;
                if (from < tomorrow)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(string.Format(ConstantHelper.Message.PriceAdjustFromTomorrow, Day(tomorrow)));
                }
                if (from < contract.EffectiveFrom.Date || (contract.EffectiveTo != null && from > contract.EffectiveTo.Value.Date) || (to != null && contract.EffectiveTo != null && to > contract.EffectiveTo.Value.Date))
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(string.Format(ConstantHelper.Message.PriceAdjustOutsideContract, Day(contract.EffectiveFrom), contract.EffectiveTo == null ? "không thời hạn" : Day(contract.EffectiveTo)));
                }
                if (to != null && to < from)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(ConstantHelper.Message.PriceAdjustToBeforeFrom);
                }
                var meals = await db.ContractMeal.AsNoTracking().Where(m => m.ContractId == contract.Id && !m.IsDeleted).ToListAsync();
                foreach (var l in lines)
                {
                    if (!meals.Any(m => string.Equals(m.MealType, l.MealType, StringComparison.OrdinalIgnoreCase)))
                    {
                        unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(string.Format(ConstantHelper.Message.PriceAdjustMealNotInContract, l.MealType));
                    }
                    string meal = l.MealType!;
                    var clash = await db.PricePolicy.AsNoTracking()
                        .Where(p => !p.IsDeleted && p.ContractId == contract.Id && p.MealType == meal && OpenStatuses.Contains(p.Status!)
                            && (p.EffectiveTo == null || p.EffectiveTo >= from) && (to == null || p.EffectiveFrom == null || p.EffectiveFrom <= to))
                        .Select(p => p.Code).FirstOrDefaultAsync();
                    if (clash != null)
                    {
                        unitOfWork.RollbackTransaction();
                    return Result<List<Guid>>.Fail(string.Format(ConstantHelper.Message.PriceAdjustOverlap, meal, clash));
                    }
                }
                var ids = new List<Guid>();
                foreach (var l in lines)
                {
                    var row = new PricePolicy
                    {
                        Id = Guid.NewGuid(), CustomerId = contract.CustomerId, ContractId = contract.Id, MealType = meals.First(m => string.Equals(m.MealType, l.MealType, StringComparison.OrdinalIgnoreCase)).MealType,
                        UnitPrice = l.UnitPrice, EffectiveFrom = from, EffectiveTo = request.EffectiveTo?.Date, Status = PriceAdjustmentStatus.Draft,
                        Name = string.IsNullOrWhiteSpace(request.Reason) ? "Phụ lục điều chỉnh giá" : request.Reason!.Trim(), CurrencyCode = "VND",
                        Code = await documentNumbers.NextCodeAsync(DocumentKind.PricePolicy),
                    };
                    unitOfWork.PricePolicyRepository.Add(row);
                    ids.Add(row.Id);
                }
                await unitOfWork.PricePolicyRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                unitOfWork.CommitTransaction();
                return Result<List<Guid>>.Success(ids, ConstantHelper.Message.PriceAdjustCreated);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<string>> Submit(Guid id)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var row = unitOfWork.PricePolicyRepository.GetSingle(p => p.Id == id && !p.IsDeleted && p.ContractId != null);
                if (row == null)
                {
                    return Result<string>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                if (row.Status != PriceAdjustmentStatus.Draft)
                {
                    return Result<string>.Fail(string.Format(ConstantHelper.Message.PriceAdjustNotDraft, PriceAdjustmentStatus.Label(row.Status)));
                }
                if ((await FileCountsAsync([id])).GetValueOrDefault(id) == 0)
                {
                    return Result<string>.Fail(ConstantHelper.Message.PriceAdjustNeedsFile);
                }
                row.Status = PriceAdjustmentStatus.PendingApproval;
                unitOfWork.PricePolicyRepository.Update(row);
                await unitOfWork.PricePolicyRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                var (request, error) = await approvals.CreateAsync(PriceAdjustmentStatus.BusinessType, row.Id);
                if (error != null)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(error);
                }
                unitOfWork.CommitTransaction();
                return Result<string>.Success(request?.RequestNo ?? string.Empty, string.Format(ConstantHelper.Message.PriceAdjustSubmitted, request?.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Submit: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<bool>> DeleteDraft(Guid id)
        {
            try
            {
                var row = unitOfWork.PricePolicyRepository.GetSingle(p => p.Id == id && !p.IsDeleted && p.ContractId != null);
                if (row == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                if (row.Status is not (PriceAdjustmentStatus.Draft or PriceAdjustmentStatus.Rejected))
                {
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.PriceAdjustNotDraft, PriceAdjustmentStatus.Label(row.Status)));
                }
                row.IsDeleted = true;
                unitOfWork.PricePolicyRepository.Update(row);
                await unitOfWork.PricePolicyRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                return Result<bool>.Success(true, ConstantHelper.Message.DeleteSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " DeleteDraft: ", ex.ToString()));
                throw;
            }
        }

        // ------------------------------------------------------------------ engine duyệt
        public async Task<string?> BuildStateAsync(Guid id)
        {
            var r = await unitOfWork.DbContext.PricePolicy.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            return r == null ? null : JsonConvert.SerializeObject(new
            {
                r.Id, r.Status, r.ContractId, r.MealType, UnitPrice = r.UnitPrice.ToString("0.##", CultureInfo.InvariantCulture),
                From = r.EffectiveFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), To = r.EffectiveTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });
        }

        public async Task<string?> DescribeAsync(Guid id)
        {
            var db = unitOfWork.DbContext;
            var r = await (from p in db.PricePolicy.AsNoTracking()
                           join c in db.Contract.AsNoTracking() on p.ContractId equals c.Id
                           join k in db.Customer.AsNoTracking() on c.CustomerId equals k.Id
                           where p.Id == id
                           select new { p.Code, p.MealType, p.UnitPrice, p.EffectiveFrom, Number = c.ContractNumber ?? c.ContractNo, Customer = k.DisplayName ?? k.Name }).FirstOrDefaultAsync();
            return r == null ? null : $"Phụ lục điều chỉnh giá {r.Code} – HĐ {r.Number} – {r.Customer} – {r.MealType} {Money(r.UnitPrice)} từ {Day(r.EffectiveFrom)}";
        }

        public async Task<ApprovalSummaryModel?> SummaryAsync(Guid id, ApprovalSummaryContext ctx)
        {
            var db = unitOfWork.DbContext;
            var r = await db.PricePolicy.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (r == null || r.ContractId == null)
            {
                return null;
            }
            var c = await db.Contract.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.ContractId);
            var k = c == null ? null : await db.Customer.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.CustomerId);
            var meals = await db.ContractMeal.AsNoTracking().Where(m => m.ContractId == r.ContractId && !m.IsDeleted).ToListAsync();
            decimal old = BaseOf(meals, r.MealType);
            var prior = await db.PricePolicy.AsNoTracking().Where(x => x.ContractId == r.ContractId && x.Id != r.Id && !x.IsDeleted && x.MealType == r.MealType
                && x.Status == PriceAdjustmentStatus.Active && x.EffectiveFrom < r.EffectiveFrom).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync();
            if (prior != null)
            {
                old = prior.UnitPrice;
            }
            decimal? pct = Pct(old, r.UnitPrice);
            int files = (await FileCountsAsync([id])).GetValueOrDefault(id);
            var s = new ApprovalSummaryModel
            {
                Title = $"Phụ lục điều chỉnh giá {r.Code} – {r.MealType}",
                TypeName = "Phụ lục điều chỉnh giá",
                ModuleName = "Kinh doanh",
                DocumentNo = r.Code,
                Counterparty = k?.DisplayName ?? k?.Name,
                MainValue = $"{Money(old)} → {Money(r.UnitPrice)}" + (pct != null ? $" ({(pct > 0 ? "+" : string.Empty)}{pct.Value.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}%)" : string.Empty),
                Reason = r.Name,
                OpenLink = c == null ? null : "#/crm-contract-detail?id=" + c.Id,
            };
            s.Info.Add(new() { Label = "Hợp đồng", Value = c?.ContractNumber ?? c?.ContractNo });
            s.Info.Add(new() { Label = "Loại suất", Value = r.MealType });
            s.Info.Add(new() { Label = "Áp dụng", Value = $"{Day(r.EffectiveFrom)} – {(r.EffectiveTo == null ? "hết hợp đồng" : Day(r.EffectiveTo))}" });
            s.Info.Add(new() { Label = "Phụ lục đã ký", Value = files == 0 ? "Chưa có" : files + " tệp" });
            s.Changes.Add(new() { Label = "Đơn giá " + r.MealType, Before = Money(old), After = Money(r.UnitPrice) });
            if (pct != null && Math.Abs(pct.Value) >= 10)
            {
                s.Flags.Add(new ApprovalFlagModel { Text = $"Giá thay đổi {(pct > 0 ? "tăng" : "giảm")} {Math.Abs(pct.Value).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}% (≥ 10%)" });
            }
            if (files == 0)
            {
                s.Flags.Add(new ApprovalFlagModel { Text = "Chưa có file phụ lục đã ký" });
            }
            return s;
        }

        public Task<string?> ApproveFromEngineAsync(Guid id, string? note)
        {
            var row = unitOfWork.PricePolicyRepository.GetSingle(p => p.Id == id && !p.IsDeleted);
            if (row == null || row.Status != PriceAdjustmentStatus.PendingApproval)
            {
                return Task.FromResult<string?>(row == null ? ConstantHelper.Message.RecordNotFoundMessage : string.Format(ConstantHelper.Message.PriceAdjustNotPending, PriceAdjustmentStatus.Label(row.Status)));
            }
            row.Status = PriceAdjustmentStatus.Active;
            unitOfWork.PricePolicyRepository.Update(row);
            return Task.FromResult<string?>(null);
        }

        public Task<string?> RejectFromEngineAsync(Guid id, string note)
        {
            var row = unitOfWork.PricePolicyRepository.GetSingle(p => p.Id == id && !p.IsDeleted);
            if (row == null || row.Status != PriceAdjustmentStatus.PendingApproval)
            {
                return Task.FromResult<string?>(row == null ? ConstantHelper.Message.RecordNotFoundMessage : string.Format(ConstantHelper.Message.PriceAdjustNotPending, PriceAdjustmentStatus.Label(row.Status)));
            }
            row.Status = PriceAdjustmentStatus.Rejected;
            row.Name = (row.Name ?? string.Empty) + " | Từ chối: " + note;
            unitOfWork.PricePolicyRepository.Update(row);
            return Task.FromResult<string?>(null);
        }
    }
}
