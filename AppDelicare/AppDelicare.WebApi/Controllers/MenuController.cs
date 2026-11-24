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
    public class MenuController(IMenuService service) : BaseController
    {
        private readonly IMenuService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] MenuSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] MenuRequest request)
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
        public async Task<IActionResult> Update([FromForm] MenuRequest request)
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
                MenuRequest request = new()
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

        // ============================================================ T13 + T46B.1 – thực đơn tuần (spec 6.2)
        /// <summary>T13: lịch tuần (thứ × loại suất / lựa chọn / nhóm món) + giá vốn / suất + FC% (khi có giá bán: nhập tay hoặc tra theo khách).</summary>
        [HttpGet("GetCalendar")]
        public async Task<IActionResult> GetCalendar(Guid id, string? mealType = null, decimal? sellingPrice = null, Guid? customerId = null, Guid? contractId = null, DateTime? on = null)
        {
            try
            {
                return Ok(await _service.GetCalendar(id, mealType, sellingPrice, customerId, contractId, on));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCalendar: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: Nháp → Đã duyệt. Quyền duyệt = mục "Phê duyệt › Thực đơn &amp; phiên bản" (#/wf-inbox?type=menu-menu, D-31).</summary>
        [Security.Permission("menu-menu", PermissionAction.Approve)]
        [HttpPost("Approve")]
        public async Task<IActionResult> Approve([FromForm] MenuActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Approve(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Approve: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: Đã duyệt → Đã công bố (đủ món mỗi thứ × loại suất đã gán). Quyền Sửa trên menu-menu (PROPOSED).</summary>
        [Security.Permission("menu-menu", PermissionAction.Edit)]
        [HttpPost("Publish")]
        public async Task<IActionResult> Publish([FromForm] MenuActionRequest request)
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

        /// <summary>T13: Đã duyệt / Đã công bố → Hết hiệu lực. Quyền Sửa trên menu-menu (PROPOSED).</summary>
        [Security.Permission("menu-menu", PermissionAction.Edit)]
        [HttpPost("Expire")]
        public async Task<IActionResult> Expire([FromForm] MenuActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Expire(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Expire: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: sao chép thực đơn thành bản nháp mới (mã mới, hiệu lực mới) kèm mọi món. Quyền Thêm trên menu-menu.</summary>
        [Security.Permission("menu-menu", PermissionAction.Add)]
        [HttpPost("Clone")]
        public async Task<IActionResult> Clone([FromForm] MenuCloneRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Clone(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Clone: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T46B.1: tải "Thực đơn gửi khách" (.xlsx) theo mẫu thật. Lỗi nghiệp vụ → JSON Result (responseCode 400). Quyền Xuất trên menu-menu.</summary>
        [HttpGet("ExportForCustomer")]
        public async Task<IActionResult> ExportForCustomer(Guid id, string? project = null)
        {
            try
            {
                var result = await _service.ExportForCustomer(id, project);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Thuc_don_gui_khach_{id:N}.xlsx");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportForCustomer: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11 (D-89): lưới thực đơn tuần (buổi → lựa chọn → nhóm món × 7 ngày) + đánh dấu + giá vốn / FC% + 4 chỉ số. Quyền Xem.</summary>
        [HttpGet("GetWeek")]
        public async Task<IActionResult> GetWeek([FromQuery] AppDelicare.Model.Model.MenuWeekRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetWeek(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeek: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11: lưu 1 ô (JSON) – tạo / đổi / xóa món; chỉ Nháp. Quyền Sửa.</summary>
        [HttpPost("SaveCell")]
        public async Task<IActionResult> SaveCell([FromBody] AppDelicare.Model.Model.MenuCellRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.SaveCell(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveCell: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11: điền cả hàng 7 ngày cùng món (JSON). Quyền Sửa.</summary>
        [HttpPost("FillRow")]
        public async Task<IActionResult> FillRow([FromBody] AppDelicare.Model.Model.MenuFillRowRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.FillRow(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " FillRow: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11: chép cả cột ngày sang ngày khác (JSON). Quyền Sửa.</summary>
        [HttpPost("CopyDay")]
        public async Task<IActionResult> CopyDay([FromBody] AppDelicare.Model.Model.MenuCopyDayRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CopyDay(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyDay: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11: 5 gợi ý món cho ô (cùng nhóm → có công thức → chưa dùng 14 ngày → giữ FC) + FC% mới. Quyền Xem.</summary>
        [HttpGet("SuggestDish")]
        public async Task<IActionResult> SuggestDish([FromQuery] AppDelicare.Model.Model.MenuSuggestRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.SuggestDish(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SuggestDish: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11: gửi duyệt – khóa khi còn ô trống / món thiếu công thức / FC vượt; hợp lệ → báo COO. Quyền Sửa.</summary>
        [HttpPost("SubmitWeek")]
        public async Task<IActionResult> SubmitWeek([FromForm] MenuActionRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.SubmitWeek(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SubmitWeek: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.11: tạo thực đơn tuần – nhân bản tuần N − 4 (mặc định) / tuần chọn (SourceId) / khung trống (Blank). Form. Quyền Thêm.</summary>
        [HttpPost("CreateWeek")]
        public async Task<IActionResult> CreateWeek([FromForm] DateTime? weekStart, [FromForm] Guid? sourceId, [FromForm] bool blank)
        {
            try
            {
                return Ok(await _service.AutoDraft(weekStart, sourceId, blank));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateWeek: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.1 (spec 6.2 GET menus): thực đơn + số dòng / món, số khách được phân công, suất đã đặt.</summary>
        [HttpGet("GetList")]
        public async Task<IActionResult> GetList([FromQuery] MenuListRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetList(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
