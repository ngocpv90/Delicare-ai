using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE3.2 (Catalog 166, spec 9.3 integration/iot/temperature – PROPOSED D-57 f): nhận lần đo nhiệt độ từ cảm biến (đã qua kiểm API key ở WebApi).
    /// DeviceCode → khu vực / vị trí kho theo <c>Iot:Devices</c>; ngưỡng = <c>StorageZone.TemperatureMin / Max</c>, <c>IsWithinRange</c> tính ở server;
    /// ghi <c>TemperatureLog</c> (SourceChannel = IOT, RecordedBy / CreatedBy = tài khoản <c>system</c>). Chống trùng (DeviceCode, MeasuredAt):
    /// đã có (hoặc lặp trong cùng lần gửi) → bỏ qua. Lần đo lỗi → <c>rejected</c> kèm lý do, phần còn lại vẫn ghi. Không gửi tin (ngoài ngưỡng → chuông BE3.3).
    /// </summary>
    public class IotTemperatureService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration
        , IOptionsMonitor<IotOptions> iot) : IIotTemperatureService
    {
        public const string SourceIot = "IOT";

        public async Task<IResult<IotReceiveResultModel>> Receive(List<IotTemperatureItem>? items)
        {
            var options = iot.CurrentValue;
            var result = new IotReceiveResultModel { Received = items?.Count ?? 0 };
            if (items == null || items.Count == 0)
            {
                return Result<IotReceiveResultModel>.Fail(ConstantHelper.Message.IotEmpty, result);
            }
            if (items.Count > options.MaxItemsPerRequest)
            {
                return Result<IotReceiveResultModel>.Fail(string.Format(ConstantHelper.Message.IotTooMany, options.MaxItemsPerRequest), result);
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid systemUser) || systemUser == Guid.Empty)
            {
                return Result<IotReceiveResultModel>.Fail(ConstantHelper.Message.JobUserMissing, result);
            }
            try
            {
                var db = unitOfWork.DbContext;
                DateTime now = clock.Now;
                // Thiết bị → khu vực (đọc 1 lần cho cả lô).
                var zoneCodes = options.Devices.Values.Where(d => d.StorageZoneId == null && !string.IsNullOrWhiteSpace(d.StorageZoneCode))
                    .Select(d => d.StorageZoneCode!.Trim()).Distinct().ToList();
                var zoneIds = options.Devices.Values.Where(d => d.StorageZoneId != null).Select(d => d.StorageZoneId!.Value).Distinct().ToList();
                var zones = await db.StorageZone.AsNoTracking()
                    .Where(z => !z.IsDeleted && (zoneIds.Contains(z.Id) || (z.Code != null && zoneCodes.Contains(z.Code))))
                    .ToListAsync();
                string? DeviceError(IotDeviceOptions device, out StorageZone? zone)
                {
                    zone = null;
                    if (!device.Enabled)
                    {
                        return ConstantHelper.Message.IotDeviceDisabled;
                    }
                    if (device.StorageZoneId is { } id)
                    {
                        zone = zones.FirstOrDefault(z => z.Id == id);
                        return zone == null ? ConstantHelper.Message.IotDeviceZoneMissing : null;
                    }
                    var matches = zones.Where(z => string.Equals(z.Code, device.StorageZoneCode?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                    zone = matches.Count == 1 ? matches[0] : null;
                    return matches.Count == 1 ? null : matches.Count == 0 ? ConstantHelper.Message.IotDeviceZoneMissing : ConstantHelper.Message.IotDeviceZoneAmbiguous;
                }

                var valid = new List<(int Index, string Device, DateTime At, decimal Temp, IotDeviceOptions Options, StorageZone Zone)>();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    string device = item.DeviceCode?.Trim() ?? string.Empty;
                    string? reason = null;
                    IotDeviceOptions? deviceOptions = null;
                    StorageZone? zone = null;
                    DateTime at = default;
                    if (device.Length == 0)
                    {
                        reason = ConstantHelper.Message.IotDeviceRequired;
                    }
                    else if ((deviceOptions = options.Devices.FirstOrDefault(d => string.Equals(d.Key, device, StringComparison.OrdinalIgnoreCase)).Value) == null)
                    {
                        reason = ConstantHelper.Message.IotDeviceUnknown;
                    }
                    else if (DeviceError(deviceOptions, out zone) is { } deviceError)
                    {
                        reason = deviceError;
                    }
                    else if (item.MeasuredAt is not { } measured)
                    {
                        reason = ConstantHelper.Message.IotMeasuredAtRequired;
                    }
                    else if ((at = ToLocal(measured)) > now.AddMinutes(options.MaxFutureMinutes) || at < now.AddDays(-options.MaxAgeDays))
                    {
                        reason = string.Format(ConstantHelper.Message.IotMeasuredAtOutOfRange, options.MaxAgeDays, options.MaxFutureMinutes);
                    }
                    else if (item.TemperatureC is not { } t || t < options.MinTemperatureC || t > options.MaxTemperatureC)
                    {
                        reason = string.Format(ConstantHelper.Message.IotTemperatureOutOfRange, options.MinTemperatureC, options.MaxTemperatureC);
                    }
                    if (reason != null)
                    {
                        result.Rejected.Add(new IotRejectedItemModel { Index = i, DeviceCode = item.DeviceCode, Reason = reason });
                        continue;
                    }
                    valid.Add((i, device.ToUpperInvariant(), Truncate(at), item.TemperatureC!.Value, deviceOptions!, zone!));
                }
                if (result.Rejected.Count > 0)
                {
                    Log.Warning("[IoT] từ chối {Count} lần đo: {Reasons}", result.Rejected.Count,
                        string.Join("; ", result.Rejected.GroupBy(r => r.Reason).Select(g => $"{g.Key} ({string.Join(", ", g.Select(r => r.DeviceCode).Distinct().Take(5))})")));
                }

                // Chống trùng: (DeviceCode, MeasuredAt) đã có trong DB hoặc lặp trong lần gửi này.
                var devices = valid.Select(v => v.Device).Distinct().ToList();
                DateTime minAt = valid.Count == 0 ? now : valid.Min(v => v.At), maxAt = valid.Count == 0 ? now : valid.Max(v => v.At);
                var existing = valid.Count == 0 ? [] : (await db.TemperatureLog.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.DeviceCode != null && devices.Contains(t.DeviceCode) && t.MeasuredAt >= minAt && t.MeasuredAt <= maxAt)
                    .Select(t => new { t.DeviceCode, t.MeasuredAt }).ToListAsync())
                    .Select(t => Key(t.DeviceCode!, t.MeasuredAt)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var logs = new List<TemperatureLog>();
                foreach (var v in valid)
                {
                    if (!existing.Add(Key(v.Device, v.At)))
                    {
                        result.Duplicate++;
                        continue;
                    }
                    decimal? min = v.Zone.TemperatureMin, max = v.Zone.TemperatureMax;
                    bool? within = min == null && max == null ? null : (min == null || v.Temp >= min) && (max == null || v.Temp <= max);
                    logs.Add(new TemperatureLog
                    {
                        Id = Guid.NewGuid(),
                        StorageZoneId = v.Zone.Id,
                        StorageLocationId = v.Options.StorageLocationId,
                        MeasurementType = (string.IsNullOrWhiteSpace(v.Options.MeasurementType) ? options.MeasurementType : v.Options.MeasurementType).Trim().ToUpperInvariant(),
                        TemperatureC = v.Temp,
                        MinAllowedC = min,
                        MaxAllowedC = max,
                        IsWithinRange = within,
                        MeasuredAt = v.At,
                        DeviceCode = v.Device,
                        SourceChannel = SourceIot,
                        RecordedBy = systemUser,
                    });
                    if (within == false)
                    {
                        result.OutOfRange++;
                    }
                }
                if (logs.Count > 0)
                {
                    using (currentActor.BeginScope(ActorInfo.ForSystem(systemUser)))
                    {
                        foreach (var log in logs)
                        {
                            unitOfWork.TemperatureLogRepository.Add(log);
                        }
                        await unitOfWork.TemperatureLogRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    }
                }
                result.Accepted = logs.Count;
                if (result.OutOfRange > 0)
                {
                    Log.Warning("[IoT] {Count} lần đo vượt ngưỡng khu vực ({Devices})", result.OutOfRange,
                        string.Join(", ", logs.Where(l => l.IsWithinRange == false).Select(l => l.DeviceCode).Distinct()));
                }
                return Result<IotReceiveResultModel>.Success(result, string.Format(ConstantHelper.Message.IotReceived, result.Accepted, result.Duplicate, result.Rejected.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Receive: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Không múi giờ (Kind = Unspecified) → giờ VN như gửi; có múi giờ (JSON đổi thành Utc / Local của máy chủ) → đổi về giờ VN theo IClock.</summary>
        private DateTime ToLocal(DateTime measured)
        {
            if (measured.Kind == DateTimeKind.Unspecified)
            {
                return measured;
            }
            DateTime utc = measured.Kind == DateTimeKind.Utc ? measured : measured.ToUniversalTime();
            TimeSpan vnOffset = TimeSpan.FromMinutes(Math.Round((clock.Now - clock.UtcNow).TotalMinutes / 15) * 15);
            return DateTime.SpecifyKind(utc + vnOffset, DateTimeKind.Unspecified);
        }

        /// <summary>Bỏ phần nhỏ hơn giây (cột datetime / so trùng ổn định).</summary>
        private static DateTime Truncate(DateTime at) => new(at.Ticks - at.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);

        private static string Key(string device, DateTime at) => device.ToUpperInvariant() + "|" + at.ToString("yyyyMMddHHmmss");
    }
}
