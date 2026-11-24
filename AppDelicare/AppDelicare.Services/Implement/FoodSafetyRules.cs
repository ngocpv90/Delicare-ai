using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.5 (T18 phần nghiệp vụ, spec 9.2 / 9.4) – lớp tĩnh dùng chung cho checklist ATTP và vệ sinh khay / hộp.
    /// Kết quả tiêu chí ghi thành dòng mới (giữ lịch sử – báo cáo NCR cần cả lần KHÔNG ĐẠT); dòng mới nhất của tiêu chí là kết quả hiện tại (PROPOSED D-54 c).
    /// </summary>
    public static class FoodSafetyRules
    {
        /// <summary>Bộ checklist ghi qua thao tác riêng (không qua FoodSafetyChecklistResult/Record) – giữ nguyên kiểm soát ATTP của mẻ.</summary>
        public static string? ViaAction(string? code, BusinessRulesOptions rules)
        {
            if (string.Equals(code, ProductionCode.StageChecklist, StringComparison.OrdinalIgnoreCase))
            {
                return "màn Mẻ nấu (bắt đầu / xong công đoạn)";
            }
            if (string.Equals(code, ProductionCode.BatchChecklist, StringComparison.OrdinalIgnoreCase))
            {
                return "màn Mẻ nấu (lưu mẫu, nhiệt độ lõi)";
            }
            if (string.Equals(code, rules.CleaningChecklistCode, StringComparison.OrdinalIgnoreCase))
            {
                return "màn Vệ sinh khay hộp";
            }
            return null;
        }

        /// <summary>Bộ checklist áp dụng ngày <paramref name="date"/>: không Ngừng, trong thời gian hiệu lực.</summary>
        public static bool IsEffective(FoodSafetyChecklist checklist, DateTime date)
            => !string.Equals(checklist.Status, "Inactive", StringComparison.OrdinalIgnoreCase)
               && (checklist.EffectiveFrom == null || checklist.EffectiveFrom.Value.Date <= date.Date)
               && (checklist.EffectiveTo == null || checklist.EffectiveTo.Value.Date >= date.Date);

        /// <summary>Bộ checklist đang dùng theo mã (không Ngừng, phiên bản lớn nhất).</summary>
        public static async Task<FoodSafetyChecklist?> ByCodeAsync(DatabaseContext db, string code)
            => (await db.FoodSafetyChecklist.AsNoTracking().Where(c => !c.IsDeleted && c.Code == code).ToListAsync())
                .OrderByDescending(c => !string.Equals(c.Status, "Inactive", StringComparison.OrdinalIgnoreCase)).ThenByDescending(c => c.VersionNo).FirstOrDefault();

        public static Task<List<FoodSafetyChecklistItem>> ItemsAsync(DatabaseContext db, Guid checklistId)
            => db.FoodSafetyChecklistItem.AsNoTracking().Where(i => !i.IsDeleted && i.ChecklistId == checklistId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.ItemCode).ToListAsync();

        /// <summary>Kết quả đã ghi của 1 lần chạy (bộ, ngày, ca, mẻ) – theo thời gian.</summary>
        public static Task<List<FoodSafetyChecklistResult>> RunResultsAsync(DatabaseContext db, Guid checklistId, DateTime date, Guid? shiftId, Guid? batchId)
        {
            DateTime day = date.Date;
            return db.FoodSafetyChecklistResult.AsNoTracking()
                .Where(r => !r.IsDeleted && r.ChecklistId == checklistId && r.CheckDate == day && r.ShiftId == shiftId && r.ProductionBatchId == batchId)
                .OrderBy(r => r.CheckedAt).ThenBy(r => r.CreatedDate).ToListAsync();
        }

        /// <summary>Dòng mới nhất theo tiêu chí (cùng giây → dòng sau trong danh sách đã sắp).</summary>
        public static Dictionary<Guid, T> Latest<T>(IEnumerable<T> ordered, Func<T, Guid?> itemOf) where T : class
        {
            var latest = new Dictionary<Guid, T>();
            foreach (var r in ordered)
            {
                if (itemOf(r) is { } item)
                {
                    latest[item] = r;
                }
            }
            return latest;
        }

        /// <summary>
        /// Giờ ghi của lần kiểm mới: bây giờ, nhưng luôn sau lần ghi trước của cùng tiêu chí (cột CheckedAt chỉ tới giây – kiểm lại ngay sau khắc phục
        /// phải là dòng mới nhất).
        /// </summary>
        public static DateTime NextCheckedAt(DateTime now, DateTime? previous)
        {
            DateTime at = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
            return previous is { } p && p >= at ? p.AddSeconds(1) : at;
        }

        /// <summary>
        /// Tiêu chí trọng yếu (IsCritical) đang KHÔNG ĐẠT của từng mẻ: kết quả mới nhất của (mẻ, tiêu chí) là FAIL – mọi bộ checklist trừ công đoạn (sự kiện STARTED / DONE).
        /// Kiểm lại ĐẠT (dòng mới PASS) thì hết chặn.
        /// </summary>
        public static async Task<Dictionary<Guid, List<string>>> OpenCriticalFailuresAsync(DatabaseContext db, IReadOnlyCollection<Guid> batchIds)
        {
            var result = new Dictionary<Guid, List<string>>();
            if (batchIds.Count == 0)
            {
                return result;
            }
            var critical = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => !i.IsDeleted && i.IsCritical)
                .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToListAsync();
            if (critical.Count == 0)
            {
                return result;
            }
            var ids = critical.Select(i => i.Id).ToList();
            var rows = await db.FoodSafetyChecklistResult.AsNoTracking()
                .Where(r => !r.IsDeleted && r.ProductionBatchId != null && batchIds.Contains(r.ProductionBatchId.Value) && ids.Contains(r.ChecklistItemId)
                    && (r.ResultCode == FoodSafetyCode.ResultPass || r.ResultCode == FoodSafetyCode.ResultFail))
                .OrderBy(r => r.CheckedAt).ThenBy(r => r.CreatedDate)
                .Select(r => new { BatchId = r.ProductionBatchId!.Value, r.ChecklistItemId, r.ResultCode }).ToListAsync();
            var latest = new Dictionary<(Guid, Guid), string?>();
            foreach (var r in rows)
            {
                latest[(r.BatchId, r.ChecklistItemId)] = r.ResultCode;
            }
            foreach (var ((batchId, itemId), code) in latest)
            {
                if (code != FoodSafetyCode.ResultFail)
                {
                    continue;
                }
                var item = critical.First(i => i.Id == itemId);
                if (!result.TryGetValue(batchId, out var list))
                {
                    result[batchId] = list = [];
                }
                list.Add(item.ItemName ?? item.ItemCode ?? itemId.ToString());
            }
            return result;
        }

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
