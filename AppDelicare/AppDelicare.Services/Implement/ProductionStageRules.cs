using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.4 (T17 "công đoạn theo tổ", ORG_STRUCTURE.md: Sơ chế → Nấu → Cơm / Canh → Chia suất / Đóng gói) – lớp tĩnh dùng chung.
    /// Không có bảng công đoạn (GAP-PRD-09): danh mục công đoạn = mục của checklist <c>PRD_STAGE</c> (ItemCode, SortOrder = thứ tự, IsRequired);
    /// sự kiện = <c>FoodSafetyChecklistResult</c> gắn mẻ (sổ ghi thêm): 1 dòng <c>STARTED</c> + 1 dòng <c>DONE</c> mỗi công đoạn, <c>WorkstationId</c> = trạm bếp,
    /// <c>CheckedBy / CheckedAt</c> = người bấm / giờ, <c>ActualValue</c> = JSON {teamId, emp[]} (tổ = OrganizationUnit loại TEAM, người làm = Employee).
    /// PROPOSED – BA/PO CONFIRM (D-53 e).
    /// </summary>
    public static class ProductionStageRules
    {
        /// <summary>Nội dung ActualValue của sự kiện công đoạn.</summary>
        public sealed class StagePayload
        {
            [JsonProperty("teamId")]
            public Guid? TeamId { get; set; }
            [JsonProperty("emp")]
            public List<Guid> EmployeeIds { get; set; } = [];
        }

        public const int MaxEmployees = 10;

        public static StagePayload ReadPayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
            {
                return new StagePayload();
            }
            try
            {
                return JsonConvert.DeserializeObject<StagePayload>(json) ?? new StagePayload();
            }
            catch (JsonException)
            {
                return new StagePayload();
            }
        }

        public static string WritePayload(StagePayload payload) => JsonConvert.SerializeObject(payload);

        /// <summary>Bộ checklist PRD_STAGE đang dùng (không Inactive, phiên bản lớn nhất) + mục theo thứ tự.</summary>
        public static async Task<(FoodSafetyChecklist? Checklist, List<FoodSafetyChecklistItem> Items)> StageItemsAsync(DatabaseContext db)
        {
            var checklist = (await db.FoodSafetyChecklist.AsNoTracking().Where(c => !c.IsDeleted && c.Code == ProductionCode.StageChecklist).ToListAsync())
                .OrderByDescending(c => c.Status != "Inactive").ThenByDescending(c => c.VersionNo).FirstOrDefault();
            if (checklist == null)
            {
                return (null, []);
            }
            var items = await db.FoodSafetyChecklistItem.AsNoTracking().Where(i => !i.IsDeleted && i.ChecklistId == checklist.Id)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.ItemCode).ToListAsync();
            return (checklist, items);
        }

        /// <summary>Sự kiện công đoạn của các mẻ (theo giờ).</summary>
        public static async Task<List<FoodSafetyChecklistResult>> EventsAsync(DatabaseContext db, IReadOnlyCollection<Guid> batchIds, IReadOnlyCollection<Guid> itemIds)
            => await db.FoodSafetyChecklistResult.AsNoTracking()
                .Where(r => !r.IsDeleted && r.ProductionBatchId != null && batchIds.Contains(r.ProductionBatchId.Value) && itemIds.Contains(r.ChecklistItemId)
                    && (r.ResultCode == ProductionCode.StageStarted || r.ResultCode == ProductionCode.StageDone))
                .OrderBy(r => r.CheckedAt).ThenBy(r => r.CreatedDate).ToListAsync();

        /// <summary>Công đoạn của từng mẻ (mọi mục của danh mục, kể cả chưa làm).</summary>
        public static async Task<Dictionary<Guid, List<ProductionStageModel>>> StagesAsync(DatabaseContext db, IReadOnlyCollection<Guid> batchIds, BusinessRulesOptions rules)
        {
            var result = batchIds.Distinct().ToDictionary(id => id, _ => new List<ProductionStageModel>());
            var (_, items) = await StageItemsAsync(db);
            if (items.Count == 0 || batchIds.Count == 0)
            {
                return result;
            }
            var events = await EventsAsync(db, batchIds, [.. items.Select(i => i.Id)]);
            var payloads = events.ToDictionary(e => e.Id, e => ReadPayload(e.ActualValue));
            var defaultCodes = rules.ProductionStageTeams.Values.Distinct().ToList();
            var teamIds = payloads.Values.Where(p => p.TeamId != null).Select(p => p.TeamId!.Value).Distinct().ToList();
            var teams = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted && (teamIds.Contains(o.Id) || defaultCodes.Contains(o.Code!)))
                .Select(o => new { o.Id, o.Code, o.Name }).ToListAsync();
            var employeeIds = payloads.Values.SelectMany(p => p.EmployeeIds).Distinct().ToList();
            var employees = await db.Employee.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName ?? e.Code ?? e.EmployeeCode);
            foreach (Guid batchId in result.Keys)
            {
                foreach (var item in items)
                {
                    var mine = events.Where(e => e.ProductionBatchId == batchId && e.ChecklistItemId == item.Id).ToList();
                    var started = mine.FirstOrDefault(e => e.ResultCode == ProductionCode.StageStarted);
                    var done = mine.FirstOrDefault(e => e.ResultCode == ProductionCode.StageDone);
                    var payload = started != null ? payloads[started.Id] : new StagePayload();
                    if (done != null && payloads[done.Id].EmployeeIds.Count != 0)
                    {
                        payload.EmployeeIds = payloads[done.Id].EmployeeIds;
                    }
                    var team = payload.TeamId != null ? teams.FirstOrDefault(t => t.Id == payload.TeamId)
                        : rules.ProductionStageTeams.TryGetValue(item.ItemCode ?? string.Empty, out var code) ? teams.FirstOrDefault(t => t.Code == code) : null;
                    string status = done != null ? ProductionStageStatus.Done : started != null ? ProductionStageStatus.InProgress : ProductionStageStatus.NotStarted;
                    result[batchId].Add(new ProductionStageModel
                    {
                        StageCode = item.ItemCode ?? string.Empty,
                        StageName = item.ItemName,
                        SortOrder = item.SortOrder,
                        IsRequired = item.IsRequired,
                        Status = status,
                        StatusName = ProductionStageStatus.Label(status),
                        TeamId = team?.Id,
                        TeamCode = team?.Code,
                        TeamName = team?.Name,
                        KitchenWorkstationId = started?.WorkstationId,
                        EmployeeIds = payload.EmployeeIds,
                        EmployeeNames = [.. payload.EmployeeIds.Select(id => employees.GetValueOrDefault(id) ?? id.ToString())],
                        StartedAt = started?.CheckedAt,
                        StartedBy = started?.CheckedBy,
                        FinishedAt = done?.CheckedAt,
                        FinishedBy = done?.CheckedBy,
                        Minutes = started != null && done != null ? Math.Round((decimal)(done.CheckedAt - started.CheckedAt).TotalMinutes, 2) : null,
                    });
                }
            }
            return result;
        }

        /// <summary>Mẻ có ghi công đoạn (đã bắt đầu ít nhất 1 công đoạn) – khi đó Finish / Pack kiểm công đoạn Nấu / Chia suất.</summary>
        public static bool UsesStages(IReadOnlyCollection<ProductionStageModel> stages, BusinessRulesOptions rules)
            => stages.Count != 0 && (rules.ProductionRequireStages || stages.Any(s => s.Status != ProductionStageStatus.NotStarted));

        /// <summary>Công đoạn hiện tại: đang làm, không có thì công đoạn kế tiếp chưa làm (bỏ qua công đoạn không bắt buộc chưa làm nếu sau nó đã có công đoạn làm); null = xong hết.</summary>
        public static string? Current(IReadOnlyList<ProductionStageModel> stages)
        {
            var inProgress = stages.FirstOrDefault(s => s.Status == ProductionStageStatus.InProgress);
            if (inProgress != null)
            {
                return inProgress.StageCode;
            }
            int lastTouched = stages.Select((s, i) => (s, i)).Where(x => x.s.Status != ProductionStageStatus.NotStarted).Select(x => x.i).DefaultIfEmpty(-1).Max();
            return stages.Skip(lastTouched + 1).FirstOrDefault(s => s.Status == ProductionStageStatus.NotStarted)?.StageCode;
        }

        /// <summary>Phút làm × số người (giờ công) của mẻ – dùng cho nhân công thực tế (spec 8.5).</summary>
        public static decimal LaborHours(IReadOnlyCollection<ProductionStageModel> stages)
            => stages.Where(s => s.Minutes != null).Sum(s => s.Minutes!.Value / 60m * Math.Max(1, s.EmployeeIds.Count));
    }
}
