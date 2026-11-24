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
    public class ProductionBatchController(IProductionBatchService service) : BaseController
    {
        private readonly IProductionBatchService _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] ProductionBatchSearchRequest request)
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
        public async Task<IActionResult> Insert([FromForm] ProductionBatchRequest request)
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
        public async Task<IActionResult> Update([FromForm] ProductionBatchRequest request)
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
                ProductionBatchRequest request = new()
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

        /// <summary>T17 (spec 8.3 batch-board): mẻ của ngày sản xuất + đếm theo trạng thái.</summary>
        [HttpGet("GetBoard")]
        public async Task<IActionResult> GetBoard([FromQuery] ProductionBoardRequest request)
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

        /// <summary>T17: chi tiết mẻ – nguyên liệu, phiếu xuất, nhiệt độ lõi, lưu mẫu, sản lượng, hao hụt, điều kiện còn thiếu.</summary>
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

        /// <summary>UI-6 (D-80): trang chi tiết toàn màn – quyền Xem prod-batch (PermissionConvention: Get* = Xem).</summary>
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

        /// <summary>T17: lập (và ghi sổ khi Post = true) phiếu xuất NL cho mẻ theo định lượng – lô FEFO / FIFO, chặn lô hết hạn / KHONG_DAT (quyền Thêm prod-batch).</summary>
        [HttpPost("CreateMaterialIssue")]
        public async Task<IActionResult> CreateMaterialIssue([FromForm] ProductionBatchIssueRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CreateMaterialIssue(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateMaterialIssue: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: Kế hoạch → Đang nấu.</summary>
        [HttpPost("Start")]
        public async Task<IActionResult> Start([FromForm] ProductionBatchStartRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Start(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Start: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: Đang nấu → Chờ QC.</summary>
        [HttpPost("Finish")]
        public async Task<IActionResult> Finish([FromForm] ProductionBatchFinishRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Finish(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Finish: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: ghi nhiệt độ lõi (TemperatureLog CORE, ngưỡng CoreTempMinC 70 °C).</summary>
        [HttpPost("RecordCoreTemperature")]
        public async Task<IActionResult> RecordCoreTemperature([FromForm] ProductionBatchTemperatureRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.RecordCoreTemperature(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordCoreTemperature: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: ghi lưu mẫu thức ăn (FoodSafetyChecklistResult SAMPLE_RETAINED).</summary>
        [HttpPost("RecordSampleRetained")]
        public async Task<IActionResult> RecordSampleRetained([FromForm] ProductionBatchSampleRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.RecordSampleRetained(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordSampleRetained: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: ghi sản lượng mẻ (ProductionOutput).</summary>
        [HttpPost("RecordOutput")]
        public async Task<IActionResult> RecordOutput([FromForm] ProductionBatchOutputRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.RecordOutput(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordOutput: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: ghi hao hụt / suất thừa của mẻ (MealWasteRecord).</summary>
        /// <summary>BOT-3.3 (D-72): nấu thêm – mẻ bổ sung cùng lệnh / món với mẻ gốc (quyền Thêm prod-batch).</summary>
        [HttpPost("CreateExtra")]
        public async Task<IActionResult> CreateExtra([FromForm] ProductionBatchExtraRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.CreateExtra(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateExtra: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("RecordWaste")]
        public async Task<IActionResult> RecordWaste([FromForm] ProductionBatchWasteRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.RecordWaste(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordWaste: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// T17: Chờ QC → Đạt – chặn khi nhiệt độ lõi &lt; 70 °C, chưa lưu mẫu, chưa ghi sản lượng.
        /// MENU-5.2 (D-79 a3): làm tay = quyền Duyệt "Phê duyệt › Mẻ nấu" (#/wf-inbox?type=prod-batch – QA). Đường tự Đạt của hệ thống (BIZ-2.7) gọi service, không qua quyền này.
        /// </summary>
        [Security.Permission("prod-batch", PermissionAction.Approve)]
        [HttpPost("Release")]
        public async Task<IActionResult> Release([FromForm] ProductionActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Release(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Release: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: tạm giữ mẻ chờ QC (nhận xét bắt buộc). MENU-5.2 (D-79): quyền Duyệt prod-batch.</summary>
        [Security.Permission("prod-batch", PermissionAction.Approve)]
        [HttpPost("Hold")]
        public async Task<IActionResult> Hold([FromForm] ProductionActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Hold(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Hold: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: loại mẻ → Không đạt (lý do bắt buộc, hao hụt QC_REJECT). MENU-5.2 (D-79): quyền Duyệt prod-batch.</summary>
        [Security.Permission("prod-batch", PermissionAction.Approve)]
        [HttpPost("Reject")]
        public async Task<IActionResult> Reject([FromForm] ProductionActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Reject(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reject: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: Đạt → Đã đóng gói – kiểm lại nhiệt độ lõi ≥ 70 °C; hoàn thành lệnh / kế hoạch khi đủ.</summary>
        [HttpPost("Pack")]
        public async Task<IActionResult> Pack([FromForm] ProductionActionRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Pack(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Pack: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T17: in Phiếu sản xuất (.xlsx) của mẻ.</summary>
        [HttpGet("ExportProductionSheet")]
        public async Task<IActionResult> ExportProductionSheet(Guid id)
        {
            try
            {
                var result = await _service.ExportProductionSheet(id);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Phieu_san_xuat_me_{id:N}.xlsx");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportProductionSheet: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.1 (spec 7.5 trace – chiều ngược): mẻ → phiếu xuất → lô NL → phiếu nhập → PO → NCC, kèm mẻ → đóng gói → đơn → khách.</summary>
        [HttpGet("Trace")]
        public async Task<IActionResult> Trace(Guid id)
        {
            try
            {
                return Ok(await _service.Trace(id));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Trace: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.4 (T17 công đoạn theo tổ): bắt đầu công đoạn (SO_CHE / NAU / CHIA_SUAT…) của mẻ – tổ, người làm, trạm; không chuyển khi công đoạn trước chưa xong.</summary>
        [HttpPost("StartStage")]
        public async Task<IActionResult> StartStage([FromForm] ProductionStageRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.StartStage(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " StartStage: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.4: xong công đoạn đang làm của mẻ.</summary>
        [HttpPost("FinishStage")]
        public async Task<IActionResult> FinishStage([FromForm] ProductionStageRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.FinishStage(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " FinishStage: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>BE2.4: tiến độ bếp theo tổ / công đoạn của 1 ngày sản xuất.</summary>
        [HttpGet("GetStageBoard")]
        public async Task<IActionResult> GetStageBoard([FromQuery] ProductionStageBoardRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetStageBoard(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStageBoard: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
