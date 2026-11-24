using Microsoft.Extensions.DependencyInjection;
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
    public class OrderController(IOrderService service) : BaseController
    {
        private readonly IOrderService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] OrderSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] OrderRequest request)
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
        public async Task<IActionResult> Update([FromForm] OrderRequest request)
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
                OrderRequest request = new()
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

        /// <summary>UI-6 (6.2 nhóm A – D-80): trang chi tiết toàn màn – quyền Xem crm-order (PermissionConvention: Get* = Xem).</summary>
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

        // ============================================================ T13 – đơn hàng (spec 6.6)
        /// <summary>T13: khung tạo đơn – hợp đồng Active tại ngày ăn, suất hợp đồng × điểm giao, ca mặc định, đơn giá, thực đơn.</summary>
        [HttpGet("GetNewTemplate")]
        public async Task<IActionResult> GetNewTemplate(Guid customerId, DateTime mealDate)
        {
            try
            {
                return Ok(await _service.GetNewTemplate(customerId, mealDate));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetNewTemplate: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: chi tiết đơn + dòng + thao tác được phép + giờ chốt + yêu cầu duyệt lệch suất đang chờ.</summary>
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

        /// <summary>T13: tạo đơn kèm dòng trong 1 transaction. JSON body vì có danh sách dòng lồng nhau (CLAUDE.md 4.2, như Authentication/Login).</summary>
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest request)
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

        /// <summary>T13: sửa số suất (0 = bỏ dòng). Đơn đã xác nhận lệch &gt; 30% → gửi duyệt. JSON body vì có danh sách dòng lồng nhau.</summary>
        [HttpPut("UpdateLines")]
        public async Task<IActionResult> UpdateLines([FromBody] OrderUpdateLinesRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.UpdateLines(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: Nháp → Đã gửi (trước giờ chốt).</summary>
        [HttpPost("Submit")]
        public async Task<IActionResult> Submit([FromForm] OrderActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Submit(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Submit: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: Đã gửi → Đã xác nhận (trước giờ chốt). Quyền Sửa trên crm-order (PROPOSED).</summary>
        [Security.Permission("crm-order", PermissionAction.Edit)]
        [HttpPost("Confirm")]
        public async Task<IActionResult> Confirm([FromForm] OrderActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Confirm(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Confirm: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: Đã xác nhận → Đã chốt (bấm tay). Quyền Sửa trên crm-order (thao tác vận hành, không phải duyệt – PROPOSED).</summary>
        [Security.Permission("crm-order", PermissionAction.Edit)]
        [HttpPost("Lock")]
        public async Task<IActionResult> Lock([FromForm] OrderActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Lock(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Lock: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: nút "Chốt đơn 20h" – khóa mọi đơn Đã xác nhận của ngày ăn (chỉ khi đã tới giờ chốt). Thay job OrderLock trong Pilot.</summary>
        [Security.Permission("crm-order", PermissionAction.Edit)]
        [HttpPost("LockByMealDate")]
        public async Task<IActionResult> LockByMealDate([FromForm] OrderLockByMealDateRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.LockByMealDate(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " LockByMealDate: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T13: hủy đơn (Nháp / Đã gửi / Đã xác nhận), lý do bắt buộc. Quyền Sửa trên crm-order (PROPOSED).</summary>
        [Security.Permission("crm-order", PermissionAction.Edit)]
        [HttpPost("Cancel")]
        public async Task<IActionResult> Cancel([FromForm] OrderCancelRequest request)
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

        /// <summary>BE2.1 (spec 6.6 GET orders view=board): bảng đơn theo 8 trạng thái + đếm theo trạng thái.</summary>
        [HttpGet("GetBoard")]
        public async Task<IActionResult> GetBoard([FromQuery] OrderBoardRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetBoard(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.2 (ORDER_CHANGE_AFTER_CUTOFF): xin sửa đơn sau giờ chốt – gửi duyệt kèm số suất mới / dòng mới, chỉ ghi khi được duyệt. JSON body vì có danh sách dòng lồng nhau. Quyền Sửa trên crm-order.</summary>
        [HttpPost("RequestChange")]
        public async Task<IActionResult> RequestChange([FromBody] OrderChangeAfterCutoffRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.RequestChange(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RequestChange: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>ORD-OPT-1.4 (D-74 g): lưới đăng ký suất tuần (buổi × lựa chọn × 7 ngày) của 1 điểm giao – số đã có, trạng thái / khóa từng ngày.</summary>
        [HttpGet("GetWeek")]
        public async Task<IActionResult> GetWeek([FromQuery] OrderWeekRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
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

        /// <summary>ORD-OPT-1.4: lưu nháp / xác nhận cả tuần (mỗi ngày 1 đơn; ngày đã chốt bỏ qua và báo lại). JSON body. Quyền Sửa trên crm-order.</summary>
        [HttpPost("SaveWeek")]
        public async Task<IActionResult> SaveWeek([FromBody] OrderWeekSaveRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.SaveWeek(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveWeek: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>ORD-OPT-1.4: số tuần trước làm gợi ý (chỉ trả về, không lưu).</summary>
        [HttpGet("CopyPreviousWeek")]
        public async Task<IActionResult> CopyPreviousWeek([FromQuery] OrderWeekRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CopyPreviousWeek(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CopyPreviousWeek: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>ORD-OPT-1.5 (D-74 h): phiếu đăng ký suất (Excel, có ô Số suất + mã ẩn để tải lên đọc lại).</summary>
        [HttpGet("ExportWeekSlip")]
        public async Task<IActionResult> ExportWeekSlip([FromQuery] OrderWeekRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await _service.ExportWeekSlip(request);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Phieu_dang_ky_suat_{request.WeekStart:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportWeekSlip: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// UI-6.13 (D-91 b): khách xin đổi món riêng – món của đơn; có OriginalDishId → phạm vi (đơn này + đơn của khách trong khoảng ngày) + 5 gợi ý món thay + tự duyệt từng ô.
        /// Quyền Xem Đơn đặt suất (KD làm trên trang đơn).
        /// </summary>
        [HttpGet("GetDishChange")]
        public async Task<IActionResult> GetDishChange([FromQuery] AppDelicare.Model.Model.OrderDishChangeRequest request)
        {
            try
            {
                var svc = HttpContext.RequestServices.GetRequiredService<IMenuChangeRequestService>();
                var r = await svc.GetOrderDishChange(request ?? new());
                if (r.ResponseCode == (int)HttpStatusCode.OK && r.Data?.Source is { } src)
                {
                    var sug = await HttpContext.RequestServices.GetRequiredService<IMenuService>().SuggestDish(new AppDelicare.Model.Model.MenuSuggestRequest
                    {
                        MenuId = src.MenuId, WeekStart = src.MealDate, MealType = src.MealType, OptionNo = src.OptionNo, CategoryId = src.CategoryId, Day = AppDelicare.Services.Implement.MenuService.MenuDayOf(src.MealDate),
                    });
                    r.Data.ListSuggestion = (sug.ListData ?? []).Where(x => x.DishId != src.DishId).ToList();
                }
                return Ok(r);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDishChange: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.13 (D-91 b): gửi đổi món cho đơn (và các đơn của khách trong khoảng ngày) – JSON; tự duyệt từng ô khi đủ điều kiện. Quyền Sửa Đơn đặt suất.</summary>
        [HttpPost("RequestDishChange")]
        public async Task<IActionResult> RequestDishChange([FromBody] AppDelicare.Model.Model.OrderDishChangeRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await HttpContext.RequestServices.GetRequiredService<IMenuChangeRequestService>().CreateForOrder(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RequestDishChange: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.6 (D-85): tổng quan tuần – mọi khách có hợp đồng hiệu lực × điểm giao đang hoạt động × 7 ngày (tổng suất, trạng thái, dự báo đã công bố) + 4 chỉ số.</summary>
        [HttpGet("GetWeekOverview")]
        public async Task<IActionResult> GetWeekOverview([FromQuery] AppDelicare.Model.Model.OrderWeekOverviewRequest request)
        {
            try
            {
                return Ok(await _service.GetWeekOverview(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetWeekOverview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.6: xác nhận nhiều điểm một lần (đơn Nháp / khách gửi của tuần, trước giờ chốt). JSON body. Quyền Sửa.</summary>
        [HttpPost("ConfirmWeekPoints")]
        public async Task<IActionResult> ConfirmWeekPoints([FromBody] AppDelicare.Model.Model.OrderWeekConfirmRequest request)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.ConfirmWeekPoints(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ConfirmWeekPoints: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.6: "Lấy số dự báo" – điền buổi còn trống từ dự báo đã công bố (chia lựa chọn theo tỉ lệ 4 tuần); chỉ trả về, không lưu.</summary>
        [HttpGet("FillWeekFromForecast")]
        public async Task<IActionResult> FillWeekFromForecast([FromQuery] OrderWeekRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.FillWeekFromForecast(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " FillWeekFromForecast: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.6: tải nhiều phiếu đăng ký một lần – tự nhận khách / điểm giao / tuần qua mã ẩn, lưu Nháp; phiếu sai mẫu / sai tuần báo riêng. Quyền Nhập.</summary>
        [HttpPost("ImportWeekSlips")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> ImportWeekSlips([FromForm] DateTime? weekStart, [FromForm] List<IFormFile> files)
        {
            var streams = new List<(string Name, Stream File)>();
            try
            {
                if (files == null || files.Count == 0)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                if (files.Any(f => Path.GetExtension(f.FileName).ToLowerInvariant() is not (".xlsx" or ".xlsm")))
                {
                    return BadRequest(ConstantHelper.Message.FileCorrectFormat);
                }
                streams.AddRange(files.Select(f => (f.FileName, f.OpenReadStream())));
                return Ok(await _service.ImportWeekSlips(weekStart, streams));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportWeekSlips: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
            finally
            {
                streams.ForEach(s => s.File.Dispose());
            }
        }

        /// <summary>ORD-OPT-1.5: đọc phiếu đăng ký đã điền vào lưới tuần – chưa lưu; trả ô + dòng lỗi.</summary>
        [HttpPost("ImportWeekSlip")]
        public async Task<IActionResult> ImportWeekSlip([FromForm] OrderWeekSlipImportRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request || request.File == null)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                string extension = Path.GetExtension(request.File!.FileName).ToLowerInvariant();
                if (extension is not (".xlsx" or ".xlsm"))
                {
                    return BadRequest(ConstantHelper.Message.FileCorrectFormat);
                }
                await using var stream = request.File.OpenReadStream();
                return Ok(await _service.ImportWeekSlip(request.CustomerId, request.DeliveryLocationId, request.WeekStart, stream));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportWeekSlip: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
