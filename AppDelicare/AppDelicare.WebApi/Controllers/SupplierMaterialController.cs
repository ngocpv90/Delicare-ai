using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class SupplierMaterialController(ISupplierMaterialService service) : BaseController
    {
        private readonly ISupplierMaterialService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] SupplierMaterialSearchRequest request)
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

        /// <summary>UI-6.19 (D-100): lưu giá mới từ ngày (không ghi đè; giá đang mở tự kết thúc hôm trước). Form. Quyền Sửa.</summary>
        [HttpPost("SaveNewPrice")]
        public async Task<IActionResult> SaveNewPrice([FromForm] AppDelicare.Model.Request.SupplierMaterialRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.SaveNewPrice(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveNewPrice: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.19: lịch sử giá NCC × nguyên liệu (quy về ĐVT kho, % thay đổi). Quyền Xem.</summary>
        [HttpGet("GetPriceHistory")]
        public async Task<IActionResult> GetPriceHistory(Guid supplierId, Guid ingredientId)
        {
            try
            {
                return Ok(await _service.GetPriceHistory(supplierId, ingredientId));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPriceHistory: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.19: giá NCC đang hiệu lực của 1 nguyên liệu (quy về ĐVT kho, ưu tiên, rẻ nhất, % đúng hẹn). Quyền Xem.</summary>
        [HttpGet("GetCompare")]
        public async Task<IActionResult> GetCompare(Guid ingredientId, DateTime? date)
        {
            try
            {
                return Ok(await _service.GetCompare(ingredientId, date));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCompare: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.19: bảng so sánh nguyên liệu × NCC tại 1 ngày + cờ việc cần làm + 4 chỉ số. Quyền Xem.</summary>
        [HttpGet("GetMatrix")]
        public async Task<IActionResult> GetMatrix([FromQuery] AppDelicare.Model.Model.SupplierPriceMatrixRequest request)
        {
            try
            {
                return Ok(await _service.GetMatrix(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.19: gia hạn hàng loạt (đặt / bỏ ngày kết thúc). JSON. Quyền Sửa.</summary>
        [HttpPost("ExtendMany")]
        public async Task<IActionResult> ExtendMany([FromBody] AppDelicare.Model.Model.SupplierPriceExtendRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.ExtendMany(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExtendMany: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.19: mẫu báo giá Excel của 1 NCC (giá hiện hành + cột giá mới). Quyền Xem.</summary>
        [HttpGet("GetQuoteTemplate")]
        public async Task<IActionResult> GetQuoteTemplate(Guid supplierId)
        {
            try
            {
                var r = await _service.ExportQuoteTemplate(supplierId);
                return r.ResponseCode != 200 || r.Data == null ? Ok(r) : File(r.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoGia_{supplierId:N}.xlsx");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetQuoteTemplate: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.19: nhập báo giá Excel – apply=false xem trước (tăng / giảm / mới / lỗi), apply=true tạo giá mới theo luật B. Form. Quyền Nhập.</summary>
        [HttpPost("ImportQuote")]
        public async Task<IActionResult> ImportQuote([FromForm] Guid supplierId, [FromForm] bool apply, IFormFile? file)
        {
            try
            {
                if (file == null) return BadRequest(ConstantHelper.Message.FileCorrectFormat);
                await using var s = file.OpenReadStream();
                using var copy = new MemoryStream();
                await s.CopyToAsync(copy);
                copy.Position = 0;
                return Ok(await _service.ImportQuote(supplierId, copy, apply));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportQuote: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("Insert")]
        public async Task<IActionResult> Insert([FromForm] SupplierMaterialRequest request)
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
        public async Task<IActionResult> Update([FromForm] SupplierMaterialRequest request)
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
                SupplierMaterialRequest request = new()
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

        /// <summary>UI-6.51 (D-137 g): lưu tỷ lệ chia đơn các NCC của một nguyên liệu (tổng = 100%).</summary>
        [HttpPost("SaveAllocation")]
        public async Task<IActionResult> SaveAllocation([FromBody] AppDelicare.Model.Request.Custom.SupplierMaterialAllocationRequest request)
        {
            try
            {
                return Ok(await _service.SaveAllocation(request ?? new AppDelicare.Model.Request.Custom.SupplierMaterialAllocationRequest()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveAllocation: ", ex.ToString()));
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
    }
}
