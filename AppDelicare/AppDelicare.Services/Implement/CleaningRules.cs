using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.5 (T20, spec 9.4 / 10.4, Catalog 167–169, 195) – lớp tĩnh dùng chung cho mẻ vệ sinh khay / hộp <c>CleaningBatche</c> (số <c>VS-YYMMDD-&lt;CA&gt;</c>):
    /// thu hồi → kiểm tra → vệ sinh 6 bước (checklist <c>VS-KHAY</c>, kết quả ở <c>CleaningChecklistResult</c>) → QC → sẵn sàng (sổ bao bì AVAILABLE).
    /// PROPOSED – BA/PO CONFIRM (D-54 f, g).
    /// </summary>
    public static class CleaningRules
    {
        /// <summary>Ca theo giờ hiện tại: ca có khung giờ chứa giờ này (kể cả ca qua đêm), không có thì ca bắt đầu gần nhất trước đó, không có thì ca đầu tiên.</summary>
        public static async Task<Shift?> CurrentShiftAsync(DatabaseContext db, DateTime now)
        {
            var shifts = await db.Shift.AsNoTracking().Where(s => !s.IsDeleted && s.StartTime != null && s.Code != null).OrderBy(s => s.StartTime).ToListAsync();
            if (shifts.Count == 0)
            {
                return null;
            }
            TimeSpan t = now.TimeOfDay;
            bool Covers(Shift s)
            {
                TimeSpan start = s.StartTime!.Value, end = s.EndTime ?? start;
                return start <= end ? t >= start && t < end : t >= start || t < end;
            }
            return shifts.FirstOrDefault(Covers) ?? shifts.LastOrDefault(s => s.StartTime <= t) ?? shifts[0];
        }

        /// <summary>Số khay / hộp dùng được của phiếu kiểm tra = trả về − hỏng (Không đạt → 0); còn lại = dùng được − đã đưa vào mẻ vệ sinh (trừ mẻ không đạt – vệ sinh lại).</summary>
        public static async Task<(int Usable, int Remaining)> UsableAsync(DatabaseContext db, ReturnInspection inspection, ContainerReturn containerReturn)
        {
            int damaged = (int)Math.Ceiling(inspection.DamagedQty ?? containerReturn.DamagedQuantity);
            int usable = inspection.InspectionResult == ReturnInspectionResult.Rejected ? 0 : Math.Max(0, containerReturn.ReturnedQuantity - damaged);
            var batches = await db.CleaningBatch.AsNoTracking().Where(b => !b.IsDeleted && b.ReturnInspectionId == inspection.Id)
                .Select(b => new { b.ContainerQuantity, b.Status, b.QcResult }).ToListAsync();
            var pending = db.ChangeTracker.Entries<CleaningBatch>().Where(e => e.State == EntityState.Added && e.Entity.ReturnInspectionId == inspection.Id)
                .Select(e => new { e.Entity.ContainerQuantity, e.Entity.Status, e.Entity.QcResult });
            int used = batches.Concat(pending).Where(b => !(b.Status == CleaningBatchStatus.Completed && b.QcResult == CleaningBatchStatus.QcReject)).Sum(b => b.ContainerQuantity);
            return (usable, Math.Max(0, usable - used));
        }

        /// <summary>Lập mẻ vệ sinh Đang vệ sinh (số VS- theo ngày + ca, lần sau trong ca thêm "-2", "-3"…).</summary>
        public static async Task<CleaningBatch> NewBatchAsync(IUnitOfWork uow, IDocumentNumberService numbers, DateTime now, Guid inspectionId, int quantity,
            Guid? responsibleEmployeeId, Shift? shift)
        {
            string suffix = shift?.Code ?? (await CurrentShiftAsync(uow.DbContext, now))?.Code ?? "CA";
            string number = await numbers.NextAsync(DocumentKind.CleaningBatch, now, suffix);
            var batch = new CleaningBatch
            {
                Id = Guid.NewGuid(),
                ReturnInspectionId = inspectionId,
                CleaningBatchNumber = number,
                CleaningNo = number,
                StartDate = now,
                StartTime = now,
                ContainerQuantity = quantity,
                Quantity = quantity,
                ResponsibleEmployeeId = responsibleEmployeeId,
                Status = CleaningBatchStatus.InProgress,
            };
            uow.CleaningBatchRepository.Add(batch);
            return batch;
        }

        public static string Number(CleaningBatch batch) => batch.CleaningBatchNumber ?? batch.CleaningNo ?? batch.Id.ToString("N")[..8];

        /// <summary>Bộ checklist vệ sinh: theo Id gửi lên, không có thì bộ <c>CleaningChecklistCode</c> đang dùng.</summary>
        public static async Task<FoodSafetyChecklist?> ChecklistAsync(DatabaseContext db, BusinessRulesOptions rules, Guid? checklistId)
            => checklistId is { } id && id != Guid.Empty
                ? await db.FoodSafetyChecklist.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted)
                : await FoodSafetyRules.ByCodeAsync(db, rules.CleaningChecklistCode);

        public static Task<List<CleaningChecklistResult>> ResultsAsync(DatabaseContext db, Guid batchId)
            => db.CleaningChecklistResult.AsNoTracking().Where(r => !r.IsDeleted && r.CleaningBatchId == batchId)
                .OrderBy(r => r.CheckedAt).ThenBy(r => r.CreatedDate).ToListAsync();

        /// <summary>Chi tiết mẻ vệ sinh: các bước (mục checklist theo SortOrder) + kết quả mới nhất, nguồn thu hồi, thao tác được phép.</summary>
        public static async Task<CleaningBatchDetailModel?> DetailAsync(DatabaseContext db, BusinessRulesOptions rules, Guid id)
        {
            var batch = await db.CleaningBatch.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);
            if (batch == null)
            {
                return null;
            }
            var inspection = await db.ReturnInspection.AsNoTracking().FirstOrDefaultAsync(r => r.Id == batch.ReturnInspectionId);
            var containerReturn = inspection == null ? null : await db.ContainerReturn.AsNoTracking().FirstOrDefaultAsync(c => c.Id == inspection.ContainerReturnId);
            var deliveryOrder = containerReturn == null ? null : await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == containerReturn.DeliveryOrderId);
            var customer = containerReturn?.CustomerId is { } cid ? await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid) : null;
            var results = await ResultsAsync(db, batch.Id);
            Guid? checklistId = results.LastOrDefault(r => r.ChecklistId != null)?.ChecklistId;
            var checklist = await ChecklistAsync(db, rules, checklistId);
            var items = checklist == null ? [] : await FoodSafetyRules.ItemsAsync(db, checklist.Id);
            var latest = FoodSafetyRules.Latest(results, r => r.ChecklistItemId);
            var userIds = latest.Values.Where(r => r.CheckedBy != null).Select(r => r.CheckedBy!.Value).Distinct().ToList();
            var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
            string? responsible = batch.ResponsibleEmployeeId is { } eid
                ? await db.Employee.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName ?? e.Code).FirstOrDefaultAsync() : null;
            var steps = items.Select(i =>
            {
                latest.TryGetValue(i.Id, out var r);
                return new CleaningStepModel
                {
                    Item = i,
                    Result = r,
                    ResultName = r == null ? null : FoodSafetyCode.ResultLabel(r.ResultCode),
                    CheckedByName = r?.CheckedBy is { } u ? users.GetValueOrDefault(u) : null,
                    HistoryCount = results.Count(x => x.ChecklistItemId == i.Id),
                    RequiresChemical = rules.CleaningChemicalRequiredItems.Contains(i.ItemCode ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                };
            }).ToList();
            var detail = new CleaningBatchDetailModel
            {
                Batch = batch,
                StatusName = CleaningBatchStatus.Label(batch.Status),
                QcResultName = batch.QcResult == null ? null : CleaningBatchStatus.QcLabel(batch.QcResult),
                PackagingType = PackagingRules.TypeOf(containerReturn?.Note),
                ResponsibleName = responsible,
                Inspection = inspection,
                ContainerReturn = containerReturn,
                CustomerName = customer?.DisplayName ?? customer?.Name,
                DeliveryNumber = deliveryOrder?.DeliveryNumber ?? deliveryOrder?.DeliveryNo,
                ChecklistId = checklist?.Id,
                ChecklistCode = checklist?.Code,
                ListStep = steps,
                StepCount = steps.Count,
                PassedStepCount = steps.Count(s => s.Result?.ResultCode == FoodSafetyCode.ResultPass),
                CanPass = steps.Count != 0 && steps.Where(s => s.Item.IsRequired).All(s => s.Result?.ResultCode == FoodSafetyCode.ResultPass),
            };
            if (batch.Status == CleaningBatchStatus.InProgress)
            {
                detail.AllowedActions = ["recordResults", "complete"];
            }
            if (checklist == null)
            {
                detail.ListWarning.Add(string.Format(ConstantHelper.Message.CleaningChecklistMissing, rules.CleaningChecklistCode));
            }
            return detail;
        }
    }
}
