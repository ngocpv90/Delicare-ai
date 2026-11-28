using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.32 (D-113): Vệ sinh khay hộp – "Đạt tất cả & kết luận" (PassAll, một giao dịch), bảng 3 cột (GetDayBoard), khay sạch cho ngày mai (GetCleanStock),
    /// Sổ vệ sinh chỉ xem (GetBook), hóa chất gợi ý. Không thêm bảng / cột.
    /// </summary>
    public partial class CleaningBatchService
    {
        private static readonly string[] OrderConfirmed = [OrderStatus.Confirmed, OrderStatus.Locked, OrderStatus.InProduction];

        private string TypeLabel(string? code) => PackagingTypeCatalog.Label(_unitOfWork.DbContext, _referenceLists, code); // PKG-FEE: tên loại từ danh mục Loại hộp / khay

        /// <summary>Hóa chất gợi ý = lần ghi bước khử khuẩn gần nhất (ưu tiên mẻ cùng loại bao bì).</summary>
        public async Task<string?> SuggestedChemicalAsync(string? packagingType)
        {
            var db = _unitOfWork.DbContext;
            var rows = await (from r in db.CleaningChecklistResult.AsNoTracking()
                              join b in db.CleaningBatch.AsNoTracking() on r.CleaningBatchId equals b.Id
                              join i in db.ReturnInspection.AsNoTracking() on b.ReturnInspectionId equals i.Id
                              join c in db.ContainerReturn.AsNoTracking() on i.ContainerReturnId equals c.Id
                              where !r.IsDeleted && r.ChemicalUsed != null && r.ChemicalUsed != ""
                              orderby r.CheckedAt descending
                              select new { r.ChemicalUsed, c.Note }).Take(30).ToListAsync();
            return (rows.FirstOrDefault(x => string.Equals(PackagingRules.TypeOf(x.Note), packagingType, StringComparison.OrdinalIgnoreCase)) ?? rows.FirstOrDefault())?.ChemicalUsed;
        }

        /// <summary>
        /// Một chạm: ghi Đạt mọi bước bắt buộc chưa Đạt (theo thứ tự, cùng người / giờ; bước khử khuẩn ghi hóa chất) rồi kết luận Đạt (sổ bao bì sẵn sàng dùng lại).
        /// Mẻ có bước đang Không đạt → chặn (làm lại bước đó trước). Lỗi bất kỳ → không ghi gì.
        /// </summary>
        public Task<IResult<CleaningBatchDetailModel>> PassAll(CleaningPassAllRequest request)
            => RunAsync("PassAll", async (actor, now, warnings) =>
            {
                var db = _unitOfWork.DbContext;
                var batch = await db.CleaningBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.Id && !b.IsDeleted);
                if (batch == null)
                {
                    return (ConstantHelper.Message.CleaningBatchNotFound, Guid.Empty);
                }
                if (batch.Status != CleaningBatchStatus.InProgress)
                {
                    return (InvalidStatus(batch, "kết luận mẻ vệ sinh"), Guid.Empty);
                }
                var detail = await CleaningRules.DetailAsync(db, _rules, batch.Id);
                var checklist = await CleaningRules.ChecklistAsync(db, _rules, detail!.ChecklistId);
                if (checklist == null)
                {
                    return (string.Format(ConstantHelper.Message.CleaningChecklistMissing, _rules.CleaningChecklistCode), Guid.Empty);
                }
                var steps = detail.ListStep.Count != 0 ? detail.ListStep
                    : (await FoodSafetyRules.ItemsAsync(db, checklist.Id)).Select(i => new CleaningStepModel
                    {
                        Item = i, RequiresChemical = _rules.CleaningChemicalRequiredItems.Contains(i.ItemCode ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                    }).ToList();
                var failed = steps.Where(s => s.Result?.ResultCode == FoodSafetyCode.ResultFail).Select(s => s.Item.ItemName ?? s.Item.ItemCode).ToList();
                if (failed.Count != 0)
                {
                    return (string.Format(ConstantHelper.Message.CleaningPassAllBlocked, CleaningRules.Number(batch), string.Join(", ", failed)), Guid.Empty);
                }
                string? chemical = FoodSafetyRules.Clean(request.ChemicalUsed);
                var pending = steps.Where(s => s.Item.IsRequired && s.Result?.ResultCode != FoodSafetyCode.ResultPass).ToList();
                foreach (var s in pending)
                {
                    if (s.RequiresChemical && chemical == null)
                    {
                        return (string.Format(ConstantHelper.Message.CleaningChemicalRequired, s.Item.ItemName ?? s.Item.ItemCode), Guid.Empty);
                    }
                    _unitOfWork.CleaningChecklistResultRepository.Add(new CleaningChecklistResult
                    {
                        Id = Guid.NewGuid(), CleaningBatchId = batch.Id, ChecklistId = checklist.Id, ChecklistItemId = s.Item.Id, ResultCode = FoodSafetyCode.ResultPass,
                        CheckedAt = FoodSafetyRules.NextCheckedAt(now, s.Result?.CheckedAt), CheckedBy = actor.UserId, ChemicalUsed = s.RequiresChemical ? chemical : null,
                    });
                }
                await _unitOfWork.CleaningChecklistResultRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                if (pending.Count != 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.CleaningPassAllDone, pending.Count));
                }
                return await CompleteCoreAsync(new CleaningCompleteRequest { Id = batch.Id, QcResult = CleaningBatchStatus.QcPass, Note = request.Note }, actor, now, warnings);
            });

        private sealed record BatchInfo(CleaningBatch Batch, Guid? CustomerId, string? CustomerName, string? PackagingType, string? DeliveryNumber);

        private async Task<List<BatchInfo>> BatchInfoAsync(List<CleaningBatch> batches)
        {
            var db = _unitOfWork.DbContext;
            var insIds = batches.Select(b => b.ReturnInspectionId).Distinct().ToList();
            var ins = await db.ReturnInspection.AsNoTracking().Where(i => insIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.ContainerReturnId);
            var crIds = ins.Values.Distinct().ToList();
            var crs = await db.ContainerReturn.AsNoTracking().Where(c => crIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
            var custIds = crs.Values.Where(c => c.CustomerId != null).Select(c => c.CustomerId!.Value).Distinct().ToList();
            var custs = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
            var doIds = crs.Values.Select(c => c.DeliveryOrderId).Distinct().ToList();
            var dos = await db.DeliveryOrder.AsNoTracking().Where(d => doIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.DeliveryNumber ?? d.DeliveryNo);
            return [.. batches.Select(b =>
            {
                var cr = ins.TryGetValue(b.ReturnInspectionId, out var crId) ? crs.GetValueOrDefault(crId) : null;
                return new BatchInfo(b, cr?.CustomerId, cr?.CustomerId is { } c ? custs.GetValueOrDefault(c) : null,
                    PackagingRules.TypeOf(cr?.Note) ?? _rules.PackagingDefaultType, cr == null ? null : dos.GetValueOrDefault(cr.DeliveryOrderId));
            })];
        }

        public async Task<IResult<CleaningDayBoardModel>> GetDayBoard(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Today).Date, next = day.AddDays(1);
                var model = new CleaningDayBoardModel { Date = day };
                // Chờ kiểm tra: phiếu thu hồi Đã thu hồi (mọi ngày – việc còn tồn).
                var waiting = await db.ContainerReturn.AsNoTracking().Where(c => !c.IsDeleted && c.Status == ContainerReturnStatus.Returned).OrderBy(c => c.ReturnDate).Take(200).ToListAsync();
                var custIds = waiting.Where(c => c.CustomerId != null).Select(c => c.CustomerId!.Value).Distinct().ToList();
                var custs = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var doIds = waiting.Select(c => c.DeliveryOrderId).Distinct().ToList();
                var dos = await db.DeliveryOrder.AsNoTracking().Where(d => doIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.DeliveryNumber ?? d.DeliveryNo);
                model.ListWaiting = [.. waiting.Select(c =>
                {
                    int missing = Math.Max(0, c.ExpectedQuantity - c.ReturnedQuantity);
                    return new CleaningWaitingCardModel
                    {
                        ContainerReturnId = c.Id, CustomerName = c.CustomerId is { } cid ? custs.GetValueOrDefault(cid) : null, PackagingType = TypeLabel(PackagingRules.TypeOf(c.Note) ?? _rules.PackagingDefaultType),
                        DeliveryNumber = dos.GetValueOrDefault(c.DeliveryOrderId), ReturnDate = c.ReturnDate, ExpectedQuantity = c.ExpectedQuantity, ReturnedQuantity = c.ReturnedQuantity,
                        DamagedQuantity = c.DamagedQuantity, MissingQuantity = missing,
                        SuggestedResult = missing > 0 ? ReturnInspectionResult.Shortage : c.DamagedQuantity > 0 ? ReturnInspectionResult.Damaged : ReturnInspectionResult.Ok,
                    };
                })];
                model.WaitingReturns = model.ListWaiting.Count;
                model.WaitingContainers = model.ListWaiting.Sum(w => Math.Max(0, w.ReturnedQuantity - w.DamagedQuantity));

                var batches = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted
                        && (b.Status == CleaningBatchStatus.InProgress || (b.Status == CleaningBatchStatus.Completed && b.EndDate >= day && b.EndDate < next)))
                    .OrderBy(b => b.StartDate).ToListAsync();
                var infos = await BatchInfoAsync(batches);
                var ids = batches.Select(b => b.Id).ToList();
                var results = await db.CleaningChecklistResult.AsNoTracking().Where(r => !r.IsDeleted && r.CleaningBatchId != null && ids.Contains(r.CleaningBatchId.Value))
                    .OrderBy(r => r.CheckedAt).ToListAsync();
                var checklist = await CleaningRules.ChecklistAsync(db, _rules, null);
                var items = checklist == null ? [] : await FoodSafetyRules.ItemsAsync(db, checklist.Id);
                var respIds = batches.Where(b => b.ResponsibleEmployeeId != null).Select(b => b.ResponsibleEmployeeId!.Value).Distinct().ToList();
                var resp = await db.Employee.AsNoTracking().Where(e => respIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName ?? e.Code);
                var insIds = batches.Select(b => b.ReturnInspectionId).Distinct().ToList();
                var later = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && insIds.Contains(b.ReturnInspectionId)).Select(b => new { b.ReturnInspectionId, b.StartDate, b.Id }).ToListAsync();
                foreach (var info in infos)
                {
                    var b = info.Batch;
                    var latest = FoodSafetyRules.Latest(results.Where(r => r.CleaningBatchId == b.Id), r => r.ChecklistItemId);
                    var card = new CleaningBatchCardModel
                    {
                        Id = b.Id, Number = CleaningRules.Number(b), CustomerName = info.CustomerName, PackagingType = TypeLabel(info.PackagingType), ContainerQuantity = b.ContainerQuantity,
                        StepCount = items.Count, PassedStepCount = items.Count(i => latest.TryGetValue(i.Id, out var r) && r.ResultCode == FoodSafetyCode.ResultPass),
                        HasFailedStep = latest.Values.Any(r => r.ResultCode == FoodSafetyCode.ResultFail),
                        ResponsibleName = b.ResponsibleEmployeeId is { } e ? resp.GetValueOrDefault(e) : null, StartDate = b.StartDate, EndDate = b.EndDate, QcResult = b.QcResult,
                        RejectReason = b.QcResult == CleaningBatchStatus.QcReject ? results.LastOrDefault(r => r.CleaningBatchId == b.Id && r.ResultCode == FoodSafetyCode.ResultFail)?.CorrectiveAction : null,
                        ReturnInspectionId = b.ReturnInspectionId,
                        Redone = later.Any(x => x.ReturnInspectionId == b.ReturnInspectionId && x.Id != b.Id && x.StartDate >= b.StartDate),
                    };
                    (b.Status == CleaningBatchStatus.InProgress ? model.ListInProgress : model.ListDone).Add(card);
                }
                model.InProgress = model.ListInProgress.Count;
                model.InProgressContainers = model.ListInProgress.Sum(x => x.ContainerQuantity);
                model.DoneTotal = model.ListDone.Count;
                model.DonePass = model.ListDone.Count(x => x.QcResult == CleaningBatchStatus.QcPass);
                model.DoneReject = model.ListDone.Count(x => x.QcResult == CleaningBatchStatus.QcReject);
                DateTime since = day.AddDays(-6);
                var week = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && b.Status == CleaningBatchStatus.Completed && b.EndDate >= since && b.EndDate < next)
                    .Select(b => b.QcResult).ToListAsync();
                model.PassRate7d = week.Count == 0 ? null : Math.Round(100m * week.Count(q => q == CleaningBatchStatus.QcPass) / week.Count, 1);
                return Result<CleaningDayBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDayBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Khay sạch cho ngày sau <paramref name="date"/>: sẵn sàng = Σ sẵn sàng dùng lại − Σ giao khách theo sổ bao bì (không âm – tồn đầu kỳ nhập bằng Ghi sổ bao bì "Sẵn sàng dùng lại"),
        /// đang vệ sinh, chờ kiểm tra; cần = suất đã chốt ngày mai × <c>PackagingUnitsPerMeal</c> theo loại bao bì của đơn.
        /// </summary>
        public async Task<IResult<CleanStockModel>> GetCleanStock(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Today).Date, forDate = day.AddDays(1);
                var model = new CleanStockModel { Date = day, ForDate = forDate };
                var bucket = new Dictionary<string, CleanStockTypeModel>(StringComparer.OrdinalIgnoreCase);
                CleanStockTypeModel Of(string? t) { t ??= _rules.PackagingDefaultType; return bucket.TryGetValue(t, out var x) ? x : bucket[t] = new CleanStockTypeModel { PackagingType = t, Label = TypeLabel(t) }; }
                var ledger = await db.PackagingLedger.AsNoTracking().Where(l => !l.IsDeleted && (l.TransactionType == PackagingTransactionType.Available || l.TransactionType == PackagingTransactionType.Out))
                    .GroupBy(l => new { l.PackagingType, l.TransactionType }).Select(g => new { g.Key.PackagingType, g.Key.TransactionType, Q = g.Sum(l => l.Quantity) }).ToListAsync();
                foreach (var g in ledger.GroupBy(x => x.PackagingType ?? _rules.PackagingDefaultType))
                {
                    Of(g.Key).Ready = Math.Max(0, g.Where(x => x.TransactionType == PackagingTransactionType.Available).Sum(x => x.Q) - g.Where(x => x.TransactionType == PackagingTransactionType.Out).Sum(x => x.Q));
                }
                var inProgress = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && b.Status == CleaningBatchStatus.InProgress).ToListAsync();
                foreach (var i in await BatchInfoAsync(inProgress))
                {
                    Of(i.PackagingType).Cleaning += i.Batch.ContainerQuantity;
                }
                foreach (var c in await db.ContainerReturn.AsNoTracking().Where(c => !c.IsDeleted && c.Status == ContainerReturnStatus.Returned).ToListAsync())
                {
                    Of(PackagingRules.TypeOf(c.Note)).WaitingInspection += Math.Max(0, c.ReturnedQuantity - c.DamagedQuantity);
                }
                DateTime end = forDate.AddDays(1);
                var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.Status != null && OrderConfirmed.Contains(o.Status)
                        && o.DeliveryDate >= forDate && o.DeliveryDate < end)
                    .Select(o => new { o.Id, o.TotalQuantity }).ToListAsync();
                foreach (var o in orders)
                {
                    Of(await PackagingRules.DefaultTypeAsync(db, o.Id, _rules)).Needed += (o.TotalQuantity ?? 0m) * _rules.PackagingUnitsPerMeal;
                }
                foreach (var t in bucket.Values)
                {
                    t.Needed = Math.Ceiling(t.Needed);
                    t.Shortage = Math.Max(0, t.Needed - t.Ready);
                }
                model.ListType = [.. bucket.Values.Where(t => t.Ready + t.Cleaning + t.WaitingInspection + t.Needed > 0).OrderByDescending(t => t.Needed).ThenBy(t => t.Label)];
                return Result<CleanStockModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCleanStock: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<CleaningBookModel>> GetBook(CleaningBookRequest request)
        {
            try
            {
                request ??= new CleaningBookRequest();
                var db = _unitOfWork.DbContext;
                DateTime dTo = (request.To ?? _clock.Today).Date, dFrom = (request.From ?? dTo.AddDays(-6)).Date;
                if (dFrom > dTo) (dFrom, dTo) = (dTo, dFrom);
                if ((dTo - dFrom).TotalDays > 93) dFrom = dTo.AddDays(-93);
                DateTime end = dTo.AddDays(1);
                var batches = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && b.Status == CleaningBatchStatus.Completed && b.EndDate >= dFrom && b.EndDate < end).ToListAsync();
                var infos = await BatchInfoAsync(batches);
                var ids = batches.Select(b => b.Id).ToList();
                var results = await db.CleaningChecklistResult.AsNoTracking().Where(r => !r.IsDeleted && r.CleaningBatchId != null && ids.Contains(r.CleaningBatchId.Value)).ToListAsync();
                var userIds = results.Where(r => r.CheckedBy != null).Select(r => r.CheckedBy!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                string kw = (request.Keyword ?? string.Empty).Trim(), res = (request.Result ?? string.Empty).Trim().ToUpperInvariant();
                var rows = infos.Select(i =>
                {
                    var rs = results.Where(r => r.CleaningBatchId == i.Batch.Id).OrderBy(r => r.CheckedAt).ToList();
                    return new CleaningBookRowModel
                    {
                        Id = i.Batch.Id, EndDate = i.Batch.EndDate, Number = CleaningRules.Number(i.Batch), CustomerId = i.CustomerId, CustomerName = i.CustomerName, PackagingType = TypeLabel(i.PackagingType),
                        ContainerQuantity = i.Batch.ContainerQuantity, QcResult = i.Batch.QcResult, Chemical = rs.LastOrDefault(r => r.ChemicalUsed != null)?.ChemicalUsed,
                        People = string.Join(", ", rs.Where(r => r.CheckedBy != null).Select(r => users.GetValueOrDefault(r.CheckedBy!.Value)).Where(n => n != null).Distinct()),
                        CorrectiveAction = string.Join("; ", rs.Where(r => r.CorrectiveAction != null).Select(r => r.CorrectiveAction).Distinct()),
                    };
                })
                .Where(r => kw.Length == 0 || (r.Number?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) || (r.CustomerName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                            || (r.Chemical?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) || (r.People?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                .Where(r => request.CustomerId == null || r.CustomerId == request.CustomerId)
                .Where(r => res.Length == 0 || r.QcResult == res)
                .OrderByDescending(r => r.EndDate).ToList();
                var model = new CleaningBookModel
                {
                    From = dFrom, To = dTo, Batches = rows.Count, Pass = rows.Count(r => r.QcResult == CleaningBatchStatus.QcPass), Reject = rows.Count(r => r.QcResult == CleaningBatchStatus.QcReject),
                    Containers = rows.Sum(r => r.ContainerQuantity),
                    ListDay = [.. rows.GroupBy(r => r.EndDate!.Value.Date).OrderByDescending(g => g.Key).Select(g => new CleaningBookDayModel
                    {
                        Date = g.Key, Batches = g.Count(), Reject = g.Count(r => r.QcResult == CleaningBatchStatus.QcReject), ListRow = [.. g],
                    })],
                };
                return Result<CleaningBookModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBook: ", ex.ToString()));
                throw;
            }
        }
    }
}
