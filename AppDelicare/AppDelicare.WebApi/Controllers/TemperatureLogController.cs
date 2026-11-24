#nullable enable
using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>Sổ chỉ ghi thêm: không có Update / Delete (CLAUDE.md 3.3, T08).</summary>
    public class TemperatureLogController(ITemperatureLogService service) : BaseController
    {
        private readonly ITemperatureLogService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] TemperatureLogSearchRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetAll(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromForm] TemperatureLogRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Save(request, ConstantHelper.TypeSave.Insert));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Insert: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetById")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                return Ok(await _service.GetById(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpGet("GetAllData")]
        public IActionResult GetAllData()
        {
            try
            {
                return Ok(_service.GetAllData());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.1 (spec 9.3 POST temperature-logs): ghi nhiệt độ tự do (khu kho / vị trí / lô / mẻ) – ngưỡng + đạt / không đạt tính ở server; ngoài ngưỡng → vẫn lưu + cảnh báo.</summary>
        [Security.Permission("qc-temperature", PermissionAction.Add)]
        [HttpPost("Record")]
        public async Task<IActionResult> Record([FromForm] TemperatureRecordRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Record(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Record: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE3.1 (Catalog 163): chuỗi nhiệt độ của 1 khu vực kho (storageZoneId, from / to – mặc định 48 giờ) + ngưỡng khu vực; quá 2 000 lần đo thì gộp theo phút.</summary>
        [HttpGet("GetSeries")]
        public async Task<IActionResult> GetSeries([FromQuery] AppDelicare.Model.Request.Custom.TemperatureSeriesRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetSeries(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSeries: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.31 (D-112): bảng "Hôm nay" theo khu bảo quản (quyền Xem qc-temperature).</summary>
        [HttpGet("GetDayBoard")]
        public async Task<IActionResult> GetDayBoard(DateTime? date)
        {
            try
            {
                return Ok(await _service.GetDayBoard(date));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDayBoard: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.31: ghi một lượt nhiều khu (quyền Thêm như Record).</summary>
        [Security.Permission("qc-temperature", PermissionAction.Add)]
        [HttpPost("RecordRound")]
        public async Task<IActionResult> RecordRound([FromBody] AppDelicare.Model.Model.TemperatureRoundRequest request)
        {
            try
            {
                return Ok(await _service.RecordRound(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordRound: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.31: Sổ nhiệt độ – chỉ xem.</summary>
        [HttpGet("GetBook")]
        public async Task<IActionResult> GetBook([FromQuery] AppDelicare.Model.Model.TemperatureBookRequest request)
        {
            try
            {
                return Ok(await _service.GetBook(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBook: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
