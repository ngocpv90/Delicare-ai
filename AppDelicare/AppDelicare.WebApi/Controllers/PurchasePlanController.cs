using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// UI-6.51 (D-137): màn "Kế hoạch mua" (thay khối Kế hoạch mua 7 ngày, giữ route / quyền <c>scm-requirement</c> – UI-6.44).
    /// Xem = Xem màn; chạy ngay / đặt = Thêm; giữ chuẩn / đổi tỷ lệ / sửa chia NCC = Sửa.
    /// </summary>
    public class PurchasePlanController(IPurchasePlanService service) : BaseController
    {
        private const string Screen = "scm-requirement";
        private readonly IPurchasePlanService _service = service;

        private async Task<IActionResult> Call<T>(string name, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Ma trận nguyên liệu × ngày cần (From mặc định hôm nay, Days mặc định 7) + 5 chỉ số.</summary>
        [HttpGet("GetBoard")]
        [Permission(Screen, PermissionAction.View)]
        public Task<IActionResult> GetBoard([FromQuery] PurchasePlanBoardRequest request) => Call(nameof(GetBoard), () => _service.GetBoard(request ?? new PurchasePlanBoardRequest()));

        /// <summary>Chi tiết 5 bước của một ô (IngredientId, NeedDate).</summary>
        [HttpGet("GetCell")]
        [Permission(Screen, PermissionAction.View)]
        public Task<IActionResult> GetCell([FromQuery] PurchasePlanCellRequest request) => Call(nameof(GetCell), () => _service.GetCell(request ?? new PurchasePlanCellRequest()));

        /// <summary>"Tính lại ngay": chạy đúng luồng job cho phần đến hạn đặt (OnlyDue) – hoặc "Đặt ngay" một ô (IngredientId + NeedDate).</summary>
        [HttpPost("Run")]
        [Permission(Screen, PermissionAction.Add)]
        public Task<IActionResult> Run([FromBody] PurchasePlanRunRequest request) => Call(nameof(Run), () => _service.Run(request ?? new PurchasePlanRunRequest()));

        /// <summary>"Đặt phần hôm nay (N)": phần đến hạn đặt tới hôm nay (như job).</summary>
        [HttpPost("OrderToday")]
        [Permission(Screen, PermissionAction.Add)]
        public Task<IActionResult> OrderToday([FromBody] PurchasePlanRunRequest? request)
            => Call(nameof(OrderToday), () => _service.Run(new PurchasePlanRunRequest { OnlyDue = true, OrganizationUnitId = request?.OrganizationUnitId, Note = request?.Note }));

        /// <summary>Giữ 100% nguyên liệu chuẩn (SharePct = 0) / đổi tỷ lệ thay thế cho một ngày cần – bắt buộc lý do, lưu lịch sử.</summary>
        [HttpPost("OverrideSubstitute")]
        [Permission(Screen, PermissionAction.Edit)]
        public Task<IActionResult> OverrideSubstitute([FromBody] PurchasePlanOverrideSubstituteRequest request) => Call(nameof(OverrideSubstitute), () => _service.OverrideSubstitute(request));

        /// <summary>Sửa phần chia NCC của (nguyên liệu, ngày cần) – tổng 100%; trống = về tỷ lệ tính. Đề nghị có phần chia sửa tay cần Trưởng mua hàng duyệt.</summary>
        [HttpPost("OverrideSplit")]
        [Permission(Screen, PermissionAction.Edit)]
        public Task<IActionResult> OverrideSplit([FromBody] PurchasePlanOverrideSplitRequest request) => Call(nameof(OverrideSplit), () => _service.OverrideSplit(request));
    }
}
