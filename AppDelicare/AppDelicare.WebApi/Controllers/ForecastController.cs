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
    public class ForecastController(IForecastService service) : BaseController
    {
        private readonly IForecastService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] ForecastSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] ForecastRequest request)
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

        [HttpPut("Update")]
        public async Task<IActionResult> Update([FromForm] ForecastRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Save(request, ConstantHelper.TypeSave.Update));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Update: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpDelete("Delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                ForecastRequest request = new()
                {
                    Id = id
                };
                return Ok(await _service.Save(request, ConstantHelper.TypeSave.Delete));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Delete: ", ex.ToString()));
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

        /// <summary>BE2.2 (spec 6.5 matrix, Catalog 87): ma trận khách × ngày – bản Đã công bố mới nhất (+ DraftValues bản Nháp chưa công bố).</summary>
        [HttpGet("GetMatrix")]
        public async Task<IActionResult> GetMatrix([FromQuery] ForecastMatrixRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetMatrix(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.2 (spec 6.5 bulk, Catalog 88): nhập hàng loạt dự báo → bản Nháp phiên bản mới. JSON body vì là danh sách dòng (CLAUDE.md 4.2). Quyền Nhập (Import) trên crm-forecast.</summary>
        [HttpPost("Import")]
        public async Task<IActionResult> Import([FromBody] ForecastImportRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Import(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Import: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.2 (spec 6.5 publish, Catalog 89): công bố bản Nháp trong khoảng ngày; bản đã công bố cũ cùng khóa → Bị thay thế. Quyền Sửa trên crm-forecast.</summary>
        [HttpPost("Publish")]
        public async Task<IActionResult> Publish([FromForm] ForecastPublishRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Publish(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Publish: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>FC-1.1 / 1.2 (D-76): hệ thống tính dự báo gợi ý (trung bình suất thực nhận cùng thứ 4 tuần) → bản Nháp. Quyền Sửa trên crm-forecast.</summary>
        [HttpPost("Suggest")]
        public async Task<IActionResult> Suggest([FromForm] ForecastSuggestRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Suggest(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Suggest: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>FC-1.5: sai số dự báo % theo khách / tuần (dự báo Đã công bố so suất thực nhận). Quyền Xem trên crm-forecast.</summary>
        [HttpGet("GetAccuracy")]
        public async Task<IActionResult> GetAccuracy([FromQuery] ForecastAccuracyRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetAccuracy(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAccuracy: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        private async Task<IActionResult> RunWeek<T>(string name, Func<Task<T>> body)
        {
            try
            {
                return Ok(await body());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.5 (D-84): dự báo theo tuần (mặc định tuần sau) – khách → điểm giao × buổi × 7 ngày.</summary>
        [HttpGet("GetWeek")]
        [Security.Permission("crm-forecast", PermissionAction.View)]
        public Task<IActionResult> GetWeek([FromQuery] AppDelicare.Model.Model.ForecastWeekRequest request) => RunWeek(nameof(GetWeek), () => _service.GetWeek(request));

        /// <summary>UI-6.5: lưu Nháp một ô (sửa trực tiếp trong bảng); số trống = xóa Nháp của ô.</summary>
        [HttpPost("SaveCell")]
        [Security.Permission("crm-forecast", PermissionAction.Edit, OrActions = [PermissionAction.Add])]
        public Task<IActionResult> SaveCell([FromBody] AppDelicare.Model.Model.ForecastCellRequest request) => RunWeek(nameof(SaveCell), () => _service.SaveCell(request));

        /// <summary>UI-6.5: tạo gợi ý cho tuần đang xem (tùy chọn 1 khách) – giữ ô người đã sửa.</summary>
        [HttpPost("SuggestWeek")]
        [Security.Permission("crm-forecast", PermissionAction.Edit, OrActions = [PermissionAction.Add])]
        public Task<IActionResult> SuggestWeek([FromForm] AppDelicare.Model.Model.ForecastWeekActionRequest request) => RunWeek(nameof(SuggestWeek), () => _service.SuggestWeek(request));

        /// <summary>UI-6.5: công bố tuần – mọi ô Nháp (bỏ qua ô khách đã đăng ký).</summary>
        [HttpPost("PublishWeek")]
        [Security.Permission("crm-forecast", PermissionAction.Edit)]
        public Task<IActionResult> PublishWeek([FromForm] AppDelicare.Model.Model.ForecastWeekActionRequest request) => RunWeek(nameof(PublishWeek), () => _service.PublishWeek(request));
    }
}
