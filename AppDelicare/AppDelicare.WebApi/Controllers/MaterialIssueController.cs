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
    public class MaterialIssueController(IMaterialIssueService service, IPrintService printService) : BaseController
    {
        private readonly IMaterialIssueService _service = service;
        private readonly IPrintService _printService = printService;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] MaterialIssueSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] MaterialIssueRequest request)
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
        public async Task<IActionResult> Update([FromForm] MaterialIssueRequest request)
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
                MaterialIssueRequest request = new()
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

        /// <summary>T16: gợi ý lô xuất FEFO / FIFO (bỏ qua lô hết hạn / QC không đạt / tạm giữ).</summary>
        [HttpGet("SuggestLots")]
        public async Task<IActionResult> SuggestLots([FromQuery] MaterialIssueSuggestRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.SuggestLots(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SuggestLots: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T16: chi tiết phiếu xuất (dòng, lô, bút toán, thao tác được phép).</summary>
        [HttpGet("GetDetail")]
        public async Task<IActionResult> GetDetail(Guid id)
        {
            try
            {
                return Ok(await _service.GetDetail(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T16: tạo phiếu xuất Nháp kèm dòng (JSON vì có danh sách dòng lồng nhau – CLAUDE.md 4.2).</summary>
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] MaterialIssueCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Create(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T16: ghi sổ phiếu xuất (Draft → Issued), mỗi dòng 1 bút toán ISSUE.</summary>
        [HttpPost("Post")]
        public async Task<IActionResult> Post([FromForm] InventoryDocumentActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Post(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Post: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T16: hủy phiếu xuất (lý do bắt buộc); phiếu đã xuất → bút toán đảo.</summary>
        [HttpPost("Cancel")]
        public async Task<IActionResult> Cancel([FromForm] MaterialIssueCancelRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Cancel(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.7 (Catalog 25): in phiếu xuất kho (chỉ khi Đã xuất) – PDF (mặc định, BE2.7.1 – cấu hình BusinessRules:Common:PrintDefaultFormat, D-56 k), format=xlsx (Excel) hoặc docx (Word). Quyền In #/scm-issue (PermissionConvention). Lỗi / chưa đủ điều kiện → JSON Result.</summary>
        [HttpGet("Print")]
        public async Task<IActionResult> Print(Guid id, string? format = null)
        {
            try
            {
                var result = await _printService.Print("MATERIAL_ISSUE", id, format);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Print: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
        /// <summary>BIZ-2.5 (D-77 – C4): phiếu xuất Nháp theo ca do hệ thống tạo khi phát hành lệnh.</summary>
        [HttpGet("GetShiftGroups")]
        public async Task<IActionResult> GetShiftGroups([FromQuery] DateTime? fromDate)
        {
            try
            {
                return Ok(await _service.GetShiftGroups(fromDate));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetShiftGroups: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BIZ-2.5: ghi sổ cả ca – mọi phiếu Nháp cùng thẻ ca (mỗi phiếu như MaterialIssue/Post). Quyền Sửa trên scm-issue.</summary>
        [HttpPost("PostShift")]
        public async Task<IActionResult> PostShift([FromForm] MaterialIssueShiftRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.PostShift(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PostShift: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
