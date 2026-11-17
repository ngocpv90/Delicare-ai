using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    public class DocumentFileController(IDocumentFileService service) : BaseController
    {
        private readonly IDocumentFileService _service = service;

        [HttpDelete("Delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                return Ok(await _service.Delete(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Delete: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        private const int Forbidden = 403;

        /// <summary>
        /// BE2.7 (Catalog 19): tải lên 1 tệp (multipart/form-data: File, Category, Resource, RecordId). Kiểm dung lượng / đuôi / nội dung (DocumentStorage),
        /// lưu ngoài wwwroot bằng tên sinh. Có Resource + RecordId → quyền Sửa màn của chứng từ; không có → quyền Thêm #/adm-document.
        /// </summary>
        [Security.SkipPermission] // màn chịu quyền theo chứng từ – service kiểm
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload([FromForm] AppDelicare.Model.Request.Custom.DocumentFileUploadRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await _service.Upload(request);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Upload: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.7 (Catalog 20): danh sách tệp – theo chứng từ (Resource + RecordId, quyền Xem màn đó) hoặc mọi tệp (quyền Xem #/adm-document).</summary>
        [Security.SkipPermission] // màn chịu quyền theo chứng từ – service kiểm
        [HttpGet("GetList")]
        public async Task<IActionResult> GetList([FromQuery] AppDelicare.Model.Request.Custom.DocumentFileListRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await _service.GetList(request);
                return result.ResponseCode == Forbidden ? StatusCode(Forbidden, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.7 (Catalog 21): tải tệp (Content-Disposition: attachment) – quyền Xem màn của chứng từ; ghi AuditChange "Download". Lỗi → JSON Result.</summary>
        [Security.SkipPermission] // màn chịu quyền theo chứng từ – service kiểm
        [HttpGet("Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            try
            {
                var result = await _service.Download(id);
                if (result.ResponseCode == Forbidden)
                {
                    return StatusCode(Forbidden, result);
                }
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Download: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
