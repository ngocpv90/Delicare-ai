using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// T45B.5: dữ liệu hỗ trợ giao diện không gắn bảng (spec 2.2) – theo mẫu controller cũ. Chỉ cần đăng nhập
    /// (dropdown / badge dùng chung mọi màn hình, không chứa dữ liệu nghiệp vụ).
    /// </summary>
    [Security.SkipPermission]
    public class MetaDataController(IMetaDataService service) : BaseController
    {
        private readonly IMetaDataService _service = service;

        /// <summary>Bộ trạng thái (47 bộ, STATUS_CATALOG – PROPOSED). key = "Order.Status" hoặc tên bảng; trống = tất cả.</summary>
        [HttpGet("GetStatuses")]
        public IActionResult GetStatuses(string? key)
        {
            try
            {
                return Ok(_service.GetStatuses(key));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStatuses: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Danh mục tham chiếu chưa có bảng (MealType, WasteType, CustomerType… – GAP-F-06, cấu hình ReferenceLists). name trống = tất cả.</summary>
        [HttpGet("GetReferenceLists")]
        public IActionResult GetReferenceLists(string? name)
        {
            try
            {
                return Ok(_service.GetReferenceLists(name));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetReferenceLists: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
