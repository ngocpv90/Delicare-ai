using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>ACC-6 (D-131 j): Đóng gói &amp; Giao hàng › Định mức bao bì (#/log-packaging-norm) – ma trận vật tư × loại suất (số / 100 suất), gửi duyệt như công thức.</summary>
    public class PackagingNormController(IPackagingNormService service) : BaseController
    {
        private readonly IPackagingNormService _service = service;

        [Security.Permission("log-packaging-norm", Services.Interface.PermissionAction.View)]
        [HttpGet("GetMatrix")]
        public Task<IActionResult> GetMatrix() => Run(nameof(GetMatrix), async () => await _service.GetMatrix());

        /// <summary>Vật tư để thêm dòng (trống = mã CC…).</summary>
        [Security.Permission("log-packaging-norm", Services.Interface.PermissionAction.View)]
        [HttpGet("GetSupplies")]
        public Task<IActionResult> GetSupplies([FromQuery] string? search) => Run(nameof(GetSupplies), async () => await _service.GetSupplies(search));

        /// <summary>Lưu cột 1 loại suất vào bản nháp (JSON { meal, listLine: [{ ingredientId, perHundred }] }).</summary>
        [Security.Permission("log-packaging-norm", Services.Interface.PermissionAction.Edit)]
        [HttpPost("SaveMeal")]
        public Task<IActionResult> SaveMeal([FromBody] PackagingNormSaveRequest request) => Run(nameof(SaveMeal), async () => await _service.SaveMeal(request ?? new PackagingNormSaveRequest()));

        /// <summary>Gửi duyệt bản nháp (JSON { meal, effectiveFrom? }) – engine RECIPE_VERSION.</summary>
        [Security.Permission("log-packaging-norm", Services.Interface.PermissionAction.Edit)]
        [HttpPost("Submit")]
        public Task<IActionResult> Submit([FromBody] PackagingNormSubmitRequest request) => Run(nameof(Submit), async () => await _service.Submit(request ?? new PackagingNormSubmitRequest()));

        /// <summary>Đề nghị mua Nháp cho bao bì thiếu của ngày × ca (JSON { day: yyyy-MM-dd, shiftCode }) – nút trên tin bot. Quyền Thêm màn Đề nghị mua.</summary>
        [Security.Permission("scm-pr", Services.Interface.PermissionAction.Add)]
        [HttpPost("CreateShortagePr")]
        public Task<IActionResult> CreateShortagePr([FromBody] PackagingShortagePrRequest request) => Run(nameof(CreateShortagePr), async () => await _service.CreateShortagePurchaseRequest((request ?? new PackagingShortagePrRequest()).Day, request?.ShiftCode));

        private async Task<IActionResult> Run(string name, Func<Task<object>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
