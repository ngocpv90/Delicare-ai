using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.31 (D-112): Nhật ký nhiệt độ – bảng "Hôm nay" theo khu (lần đo mới nhất, đường 24 giờ, trạng thái Đạt / Ngoài ngưỡng chưa khắc phục /
    /// Đến giờ chưa đo / Cảm biến mất tín hiệu), "Ghi một lượt" (một giao dịch), Sổ nhiệt độ chỉ xem, nhắc qua bot (BotWatch). Không thêm bảng / cột.
    /// Khu có cảm biến = có thiết bị bật trong <c>Iot:Devices</c>; khu ghi tay (hoặc cảm biến mất tín hiệu) phải đo ≥ 1 lần mỗi ca –
    /// quá <c>TemperatureManualDueMinutes</c> đầu ca chưa đo → "đến giờ chưa đo"; cảm biến im quá <c>IotSilenceMinutes</c> → "mất tín hiệu".
    /// </summary>
    public partial class TemperatureLogService
    {
        /// <summary>Loại đo ghi qua nghiệp vụ (lõi mẻ / giao hàng / nhận hàng) – không thuộc sổ bảo quản mặc định.</summary>
        private static readonly HashSet<string> ProcessTypes = new(StringComparer.OrdinalIgnoreCase) { ProductionCode.CoreMeasurement, "CORE_TEMP", DeliveryCode.DeliveryMeasurement, "RECEIVING" };

        private string MeasurementName(string? code)
            => _referenceLists.Lists.TryGetValue("TemperatureMeasurementType", out var list) && list.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) is { } i
                ? i.Label
                : (code ?? string.Empty).ToUpperInvariant() switch
                {
                    "CORE" or "CORE_TEMP" => "Nhiệt độ lõi mẻ",
                    "DELIVERY" => "Giao hàng",
                    "RECEIVING" => "Nhận hàng",
                    _ => code ?? string.Empty,
                };

        /// <summary>Loại đo theo loại khu (không hỏi người đo): tủ giữ nóng → giữ nóng thành phẩm, khu chia suất / đóng gói → khu chia suất, còn lại → kho / tủ bảo quản.</summary>
        private static string ZoneMeasurementType(StorageZone z)
        {
            string n = ((z.Name ?? string.Empty) + " " + (z.Code ?? string.Empty)).ToLowerInvariant();
            return n.Contains("giữ nóng") || n.Contains("hot") ? "HOT_HOLDING"
                : n.Contains("chia suất") || n.Contains("đóng gói") || n.Contains("packing") ? "PACKING_AREA"
                : "STORAGE";
        }

        private Dictionary<Guid, List<string>> SensorsByZone(List<StorageZone> zones)
        {
            var map = new Dictionary<Guid, List<string>>();
            foreach (var (code, d) in _iot.CurrentValue.Devices.Where(x => x.Value.Enabled))
            {
                var zone = d.StorageZoneId is { } id ? zones.FirstOrDefault(z => z.Id == id)
                    : zones.FirstOrDefault(z => string.Equals(z.Code, d.StorageZoneCode?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (zone != null)
                {
                    (map.TryGetValue(zone.Id, out var l) ? l : map[zone.Id] = []).Add(code);
                }
            }
            return map;
        }

        /// <summary>Ca đang chạy lúc <paramref name="now"/> và giờ bắt đầu (ca qua đêm bắt đầu từ hôm trước).</summary>
        private static (Shift? Shift, DateTime? Start) CurrentShift(List<Shift> shifts, DateTime now)
        {
            foreach (var s in shifts)
            {
                if (s.StartTime is not { } a || s.EndTime is not { } b)
                {
                    continue;
                }
                var t = now.TimeOfDay;
                if (a <= b ? t >= a && t < b : t >= a || t < b)
                {
                    return (s, a <= b || t >= a ? now.Date + a : now.Date.AddDays(-1) + a);
                }
            }
            return (null, null);
        }

        private static bool Resolved(TemperatureLog log, IEnumerable<TemperatureLog> sameZone)
            => log.IsWithinRange != false || log.CorrectiveAction != null || sameZone.Any(x => x.MeasuredAt > log.MeasuredAt && x.IsWithinRange == true);

        public async Task<IResult<TemperatureDayBoardModel>> GetDayBoard(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now, day = (date ?? now).Date;
                bool isToday = day == now.Date;
                DateTime refAt = isToday ? now : day.AddDays(1).AddSeconds(-1), since = refAt.AddDays(-7);
                var zones = (await db.StorageZone.AsNoTracking().Where(z => !z.IsDeleted).ToListAsync())
                    .Where(z => !string.Equals(z.Status, "Inactive", StringComparison.OrdinalIgnoreCase)).ToList();
                var whIds = zones.Select(z => z.WarehouseId).Distinct().ToList();
                var warehouses = await db.Warehouse.AsNoTracking().Where(w => whIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Name);
                var zoneIds = zones.Select(z => z.Id).ToList();
                var logs = (await db.TemperatureLog.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.StorageZoneId != null && zoneIds.Contains(t.StorageZoneId.Value) && t.MeasuredAt >= since && t.MeasuredAt <= refAt)
                    .OrderBy(t => t.MeasuredAt).ToListAsync())
                    .Where(t => !ProcessTypes.Contains(t.MeasurementType ?? string.Empty)).ToList();
                var userIds = logs.Where(l => l.RecordedBy != null).Select(l => l.RecordedBy!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                var shifts = (await db.Shift.AsNoTracking().Where(s => !s.IsDeleted).ToListAsync())
                    .Where(s => !string.Equals(s.Status, "Inactive", StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.StartTime).ToList();
                var (shift, shiftStart) = isToday ? CurrentShift(shifts, now) : (null, null);
                var sensors = SensorsByZone(zones);
                int silenceMin = Math.Max(1, _rules.IotSilenceMinutes), dueMin = Math.Max(0, _rules.TemperatureManualDueMinutes);
                var model = new TemperatureDayBoardModel { Date = day, Now = now, ShiftId = shift?.Id, ShiftName = shift?.Name ?? shift?.Code, ShiftStart = shiftStart };
                foreach (var z in zones.OrderBy(z => warehouses.GetValueOrDefault(z.WarehouseId)).ThenBy(z => z.Name))
                {
                    var zl = logs.Where(l => l.StorageZoneId == z.Id).ToList();
                    var last = zl.LastOrDefault();
                    sensors.TryGetValue(z.Id, out var devs);
                    var card = new TemperatureZoneCardModel
                    {
                        StorageZoneId = z.Id, ZoneName = z.Name ?? z.Code, WarehouseName = warehouses.GetValueOrDefault(z.WarehouseId), TemperatureMin = z.TemperatureMin, TemperatureMax = z.TemperatureMax,
                        MeasurementType = ZoneMeasurementType(z), HasSensor = devs is { Count: > 0 }, ListDevice = devs ?? [],
                        LastTemperature = last?.TemperatureC, LastAt = last?.MeasuredAt, LastWithin = last?.IsWithinRange,
                        LastSource = last == null ? null : string.Equals(last.SourceChannel, IotTemperatureService.SourceIot, StringComparison.OrdinalIgnoreCase) ? "IOT" : "MANUAL",
                        LastBy = last == null ? null : string.Equals(last.SourceChannel, IotTemperatureService.SourceIot, StringComparison.OrdinalIgnoreCase) ? "Cảm biến " + last.DeviceCode
                            : last.RecordedBy is { } u ? users.GetValueOrDefault(u) : null,
                        ListPoint = [.. zl.Where(l => l.MeasuredAt >= refAt.AddHours(-24)).Select(l => new TemperatureSparkPointModel { At = l.MeasuredAt, T = l.TemperatureC, Ok = l.IsWithinRange })],
                    };
                    bool hasRange = z.TemperatureMin != null || z.TemperatureMax != null;
                    bool silent = false;
                    if (card.HasSensor && isToday)
                    {
                        var lastIot = zl.LastOrDefault(l => string.Equals(l.SourceChannel, IotTemperatureService.SourceIot, StringComparison.OrdinalIgnoreCase));
                        silent = lastIot == null || lastIot.MeasuredAt < now.AddMinutes(-silenceMin);
                        card.SilentSince = lastIot?.MeasuredAt;
                    }
                    bool manual = !card.HasSensor || silent;
                    bool due = isToday && hasRange && manual && shiftStart is { } ss && now >= ss.AddMinutes(dueMin) && !zl.Any(l => l.MeasuredAt >= ss);
                    bool open = last != null && zl.Where(l => l.IsWithinRange == false).Any(l => !Resolved(l, zl));
                    card.Status = open ? "OUT" : silent ? "SILENT" : due ? "DUE" : last == null ? "NONE" : "OK";
                    model.ListZone.Add(card);
                    foreach (var o in zl.Where(l => l.IsWithinRange == false))
                    {
                        if (Resolved(o, zl)) model.Out7dResolved++; else model.Out7dOpen++;
                    }
                }
                model.ZonesOut = model.ListZone.Count(z => z.Status == "OUT");
                model.DueNotMeasured = model.ListZone.Count(z => z.Status == "DUE");
                model.SilentSensors = model.ListZone.Count(z => z.Status == "SILENT");
                return Result<TemperatureDayBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDayBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Ghi một lượt: mỗi khu một lần đo (loại đo theo loại khu, giờ đo = lúc lưu) – một giao dịch; ngoài ngưỡng thiếu khắc phục → lỗi cả lượt.</summary>
        public async Task<IResult<TemperatureRoundResultModel>> RecordRound(TemperatureRoundRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var items = (request?.ListItem ?? []).Where(i => i.StorageZoneId != Guid.Empty && i.TemperatureC != null).ToList();
                if (items.Count == 0)
                {
                    return Fail(ConstantHelper.Message.TempRoundEmpty);
                }
                if (items.GroupBy(i => i.StorageZoneId).Any(g => g.Count() > 1))
                {
                    return Fail(ConstantHelper.Message.TempRoundDuplicate);
                }
                var ids = items.Select(i => i.StorageZoneId).ToList();
                var zones = await db.StorageZone.AsNoTracking().Where(z => ids.Contains(z.Id) && !z.IsDeleted).ToDictionaryAsync(z => z.Id);
                foreach (var i in items)
                {
                    if (!zones.TryGetValue(i.StorageZoneId, out var z))
                    {
                        return Fail(string.Format(ConstantHelper.Message.TempTargetNotFound, "Khu kho"));
                    }
                    decimal t = i.TemperatureC!.Value;
                    bool outOf = (z.TemperatureMin != null && t < z.TemperatureMin) || (z.TemperatureMax != null && t > z.TemperatureMax);
                    if (ZoneMeasurementType(z) == HotHolding)
                    {
                        outOf = t < _rules.DeliveryTempMinC;
                    }
                    if (outOf && string.IsNullOrWhiteSpace(i.CorrectiveAction))
                    {
                        return Fail(string.Format(ConstantHelper.Message.TempRoundNeedCorrective, z.Name ?? z.Code, ProductionRules.Num(t)));
                    }
                }
                var model = new TemperatureRoundResultModel();
                var saved = new List<TemperatureLog>();
                foreach (var i in items)
                {
                    var z = zones[i.StorageZoneId];
                    var (log, error) = await RecordCoreAsync(new TemperatureRecordRequest
                    {
                        StorageZoneId = z.Id, MeasurementType = ZoneMeasurementType(z), TemperatureC = i.TemperatureC, CorrectiveAction = i.CorrectiveAction, SourceChannel = DeliveryCode.SourceManual,
                    }, model.ListWarning);
                    if (log == null)
                    {
                        return Fail((z.Name ?? z.Code) + ": " + error);
                    }
                    saved.Add(log);
                }
                _unitOfWork.CommitTransaction();
                model.Saved = saved.Count;
                model.OutOfRange = saved.Count(l => l.IsWithinRange == false);
                await NotifyManualOutAsync(saved, zones);
                return Result<TemperatureRoundResultModel>.Success(model, string.Format(ConstantHelper.Message.TempRoundSaved, saved.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordRound: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<TemperatureRoundResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<TemperatureRoundResultModel>.Fail(message);
            }
        }

        /// <summary>Ghi tay ngoài ngưỡng → tin bot như IoT (IOT_OUT_OF_RANGE – QA, thủ kho, trưởng ca), khóa theo khu + giờ đo.</summary>
        private async Task NotifyManualOutAsync(IEnumerable<TemperatureLog> logs, IReadOnlyDictionary<Guid, StorageZone>? zones = null)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                foreach (var l in logs.Where(x => x.IsWithinRange == false && x.StorageZoneId != null))
                {
                    var z = zones != null && zones.TryGetValue(l.StorageZoneId!.Value, out var zz) ? zz : await db.StorageZone.AsNoTracking().FirstOrDefaultAsync(x => x.Id == l.StorageZoneId);
                    string range = l.MinAllowedC != null && l.MaxAllowedC != null ? $"{ProductionRules.Num(l.MinAllowedC.Value)}…{ProductionRules.Num(l.MaxAllowedC.Value)} °C"
                        : l.MinAllowedC != null ? $"≥ {ProductionRules.Num(l.MinAllowedC.Value)} °C" : l.MaxAllowedC != null ? $"≤ {ProductionRules.Num(l.MaxAllowedC.Value)} °C" : string.Empty;
                    await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.IotOutOfRange, ["#/qc-temperature"],
                        new { title = z?.Name ?? z?.Code, detail = $"Đo tay {ProductionRules.Num(l.TemperatureC)} °C lúc {l.MeasuredAt:HH:mm} – ngoài ngưỡng {range}" + (l.CorrectiveAction == null ? string.Empty : $". Khắc phục: {l.CorrectiveAction}") },
                        $"{NotificationCatalog.IotOutOfRange}:M:{l.StorageZoneId:N}:{l.MeasuredAt:yyyyMMddHHmm}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TemperatureLog] gửi tin ngoài ngưỡng lỗi – bỏ qua");
            }
        }

        public async Task<IResult<TemperatureBookModel>> GetBook(TemperatureBookRequest request)
        {
            try
            {
                request ??= new TemperatureBookRequest();
                var db = _unitOfWork.DbContext;
                DateTime dTo = (request.To ?? _clock.Today).Date, dFrom = (request.From ?? dTo.AddDays(-6)).Date;
                if (dFrom > dTo)
                {
                    (dFrom, dTo) = (dTo, dFrom);
                }
                if ((dTo - dFrom).TotalDays > 93)
                {
                    dFrom = dTo.AddDays(-93);
                }
                DateTime end = dTo.AddDays(1);
                // Lấy thêm 2 ngày sau để biết lần ngoài ngưỡng cuối kỳ đã có lần đo đạt sau đó chưa.
                var all = await db.TemperatureLog.AsNoTracking().Where(t => !t.IsDeleted && t.MeasuredAt >= dFrom && t.MeasuredAt < end.AddDays(2)
                        && (request.StorageZoneId == null || t.StorageZoneId == request.StorageZoneId)).OrderBy(t => t.MeasuredAt).ToListAsync();
                var byZone = all.Where(t => t.StorageZoneId != null).GroupBy(t => t.StorageZoneId!.Value).ToDictionary(g => g.Key, g => g.ToList());
                var logs = all.Where(t => t.MeasuredAt < end && (request.IncludeProcess || !ProcessTypes.Contains(t.MeasurementType ?? string.Empty))).ToList();
                var zoneIds = logs.Where(l => l.StorageZoneId != null).Select(l => l.StorageZoneId!.Value).Distinct().ToList();
                var zones = await db.StorageZone.AsNoTracking().Where(z => zoneIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Name ?? z.Code);
                var batchIds = logs.Where(l => l.ProductionBatchId != null).Select(l => l.ProductionBatchId!.Value).Distinct().ToList();
                var batches = await db.ProductionBatch.AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BatchNumber ?? b.BatchNo);
                var lotIds = logs.Where(l => l.MaterialLotId != null).Select(l => l.MaterialLotId!.Value).Distinct().ToList();
                var lots = await db.MaterialLot.AsNoTracking().Where(b => lotIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.LotNumber ?? b.LotNo);
                var userIds = logs.Where(l => l.RecordedBy != null).Select(l => l.RecordedBy!.Value).Distinct().ToList();
                var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
                string kw = (request.Keyword ?? string.Empty).Trim(), src = (request.Source ?? string.Empty).Trim().ToUpperInvariant();
                var rows = logs.Select(l =>
                {
                    bool iot = string.Equals(l.SourceChannel, IotTemperatureService.SourceIot, StringComparison.OrdinalIgnoreCase);
                    var target = new List<string>();
                    if (l.StorageZoneId is { } zid && zones.GetValueOrDefault(zid) is { } zn) target.Add(zn);
                    if (l.ProductionBatchId is { } bid && batches.GetValueOrDefault(bid) is { } bn) target.Add("Mẻ " + bn);
                    if (l.MaterialLotId is { } lid && lots.GetValueOrDefault(lid) is { } ln) target.Add("Lô " + ln);
                    return new TemperatureBookRowModel
                    {
                        Id = l.Id, MeasuredAt = l.MeasuredAt, Target = string.Join(" · ", target), StorageZoneId = l.StorageZoneId, ProductionBatchId = l.ProductionBatchId, MaterialLotId = l.MaterialLotId,
                        MeasurementType = l.MeasurementType, MeasurementName = MeasurementName(l.MeasurementType), TemperatureC = l.TemperatureC, MinAllowedC = l.MinAllowedC, MaxAllowedC = l.MaxAllowedC,
                        IsWithinRange = l.IsWithinRange, Source = iot ? "IOT" : "MANUAL", By = iot ? "Cảm biến " + l.DeviceCode : l.RecordedBy is { } u ? users.GetValueOrDefault(u) : null,
                        CorrectiveAction = l.CorrectiveAction, Resolved = l.StorageZoneId is { } z && byZone.TryGetValue(z, out var zl) ? Resolved(l, zl) : l.IsWithinRange != false || l.CorrectiveAction != null,
                    };
                })
                .Where(r => kw.Length == 0 || (r.Target?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) || (r.By?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                            || (r.CorrectiveAction?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) || (r.MeasurementName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
                .Where(r => src.Length == 0 || r.Source == src)
                .Where(r => !request.OnlyOutOfRange || r.IsWithinRange == false)
                .OrderByDescending(r => r.MeasuredAt).ToList();
                var model = new TemperatureBookModel
                {
                    From = dFrom, To = dTo, Count = rows.Count, OutOfRange = rows.Count(r => r.IsWithinRange == false), OpenOutOfRange = rows.Count(r => r.IsWithinRange == false && !r.Resolved),
                    ListDay = [.. rows.GroupBy(r => r.MeasuredAt.Date).OrderByDescending(g => g.Key).Select(g => new TemperatureBookDayModel { Date = g.Key, Count = g.Count(), OutOfRange = g.Count(r => r.IsWithinRange == false), ListRow = [.. g] })],
                };
                return Result<TemperatureBookModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBook: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// BotWatch (UI-6.31): khu ghi tay "đến giờ chưa đo" → 1 tin gộp / ca (trưởng ca + QA, khóa ngày + ca); cảm biến mất tín hiệu → 1 tin / thiết bị / khoảng mất
        /// (khóa theo lần nhận cuối). Trả số tin đã xếp.
        /// </summary>
        public async Task<int> RemindAsync(DateTime now)
        {
            var board = (await GetDayBoard(now.Date)).Data;
            if (board == null)
            {
                return 0;
            }
            var db = _unitOfWork.DbContext;
            int sent = 0;
            var due = board.ListZone.Where(z => z.Status == "DUE").ToList();
            if (due.Count != 0 && board.ShiftId != null)
            {
                sent += await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.TemperatureDue, ["#/qc-temperature"],
                    new
                    {
                        title = $"Ca {board.ShiftName}: {due.Count} khu chưa đo nhiệt độ",
                        detail = $"Quá {_rules.TemperatureManualDueMinutes} phút đầu ca chưa có lần đo – mở Nhật ký nhiệt độ › Ghi một lượt",
                        lines = due.Select(z => $"🌡 {z.ZoneName}" + (z.WarehouseName == null ? string.Empty : $" ({z.WarehouseName})")).ToList(),
                    },
                    $"{NotificationCatalog.TemperatureDue}:{now:yyyyMMdd}:{board.ShiftId:N}");
            }
            foreach (var z in board.ListZone.Where(z => z.Status == "SILENT"))
            {
                sent += await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.TemperatureDue, ["#/qc-temperature"],
                    new
                    {
                        title = $"Cảm biến mất tín hiệu – {z.ZoneName}",
                        detail = (z.SilentSince == null ? "Chưa nhận số đo nào" : $"Không nhận số đo từ {z.SilentSince:HH:mm dd/MM}") + " – đo tay khu này tới khi cảm biến có lại",
                        lines = z.ListDevice.Select(d => "📡 " + d).ToList(),
                    },
                    $"{NotificationCatalog.TemperatureDue}:S:{z.StorageZoneId:N}:{z.SilentSince:yyyyMMddHHmm}");
            }
            return sent;
        }
    }
}
