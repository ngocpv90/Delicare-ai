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
    public class GoodsReceiptController(IGoodsReceiptService service, IPrintService printService) : BaseController
    {
        private readonly IGoodsReceiptService _service = service;
        private readonly IPrintService _printService = printService;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] GoodsReceiptSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] GoodsReceiptRequest request)
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
        public async Task<IActionResult> Update([FromForm] GoodsReceiptRequest request)
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
                GoodsReceiptRequest request = new()
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

        /// <summary>UI-6 (D-80): trang chi tiết toàn màn – quyền Xem scm-gr (PermissionConvention: Get* = Xem).</summary>
        [HttpGet("GetOverview")]
        public async Task<IActionResult> GetOverview(Guid id)
        {
            try
            {
                return Ok(await _service.GetOverview(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15: chi tiết phiếu nhập + kết quả QC + nhiệt độ / ngưỡng + lô đã tạo.</summary>
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

        /// <summary>UI-6.50 (D-132): nhận gộp nhiều đơn mua của 1 NCC (dòng theo nguyên liệu, máy chủ chia về từng đơn – đơn cũ trước; mỗi đơn 1 phiếu; 1 giao dịch).</summary>
        [Security.Permission("scm-gr", AppDelicare.Services.Interface.PermissionAction.Add)]
        [HttpPost("CreateMany")]
        public async Task<IActionResult> CreateMany([FromBody] GoodsReceiptCreateManyRequest request)
        {
            try
            {
                return null == request ? BadRequest(ConstantHelper.Message.ModelStateMessage) : Ok(await _service.CreateMany(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateMany: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T15 (spec 7.3 receive): lập phiếu nhập Chờ QC theo PO đã duyệt. JSON body vì có danh sách dòng lồng nhau.</summary>
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] GoodsReceiptCreateRequest request)
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

        /// <summary>T15 (spec 7.4 post): ghi sổ phiếu đã kết luận QC – lô NL-, nhập kho phần đạt, nhiệt độ nhận, sự cố NCC.</summary>
        [HttpPost("Post")]
        public async Task<IActionResult> Post([FromForm] GoodsReceiptPostRequest request)
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

        /// <summary>T15: hủy phiếu nhập chưa ghi sổ (lý do bắt buộc).</summary>
        [HttpPost("Cancel")]
        public async Task<IActionResult> Cancel([FromForm] PurchaseCancelRequest request)
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

        /// <summary>BE2.7 (Catalog 24): in phiếu nhập kho (chỉ khi QC đã kết luận) – PDF (mặc định, BE2.7.1 – cấu hình BusinessRules:Common:PrintDefaultFormat, D-56 k), format=xlsx (Excel) hoặc docx (Word). Quyền In #/scm-gr (PermissionConvention). Lỗi / chưa đủ điều kiện → JSON Result.</summary>
        [HttpGet("Print")]
        public async Task<IActionResult> Print(Guid id, string? format = null)
        {
            try
            {
                var result = await _printService.Print("GOODS_RECEIPT", id, format);
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
    }
}
