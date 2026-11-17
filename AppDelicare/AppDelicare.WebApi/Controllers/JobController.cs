using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Data.Repositories;
using AppDelicare.Services.Jobs;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// T23 (D-48): job tự động. Job chạy theo giờ trong <b>AppDelicare.Job</b> (container / Windows Service riêng – không chạy trong WebApi);
    /// controller này chỉ xem danh sách và <b>chạy tay</b> (gọi thẳng Service, cùng khóa chống chạy trùng). Chỉ admin.
    /// </summary>
    public class JobController(IJobRunService service) : BaseController
    {
        private readonly IJobRunService _service = service;

        /// <summary>Danh sách job: bật / tắt, giờ chạy (giờ VN), lần chạy kế tiếp, lần chạy gần nhất (AnalyticsRefreshLog).</summary>
        [Security.AdminOnly]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _service.GetJobs();
                return Ok(new Results<JobInfoModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count,
                });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Chạy tay 1 job ngay (form: <c>jobCode</c>), dưới tài khoản người bấm. Job đang chạy ở nơi khác → 400.</summary>
        [Security.AdminOnly]
        [HttpPost("Run")]
        public async Task<IActionResult> Run([FromForm] string jobCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobCode))
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Run(jobCode, JobTrigger.Manual, HttpContext.RequestAborted));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Run: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
