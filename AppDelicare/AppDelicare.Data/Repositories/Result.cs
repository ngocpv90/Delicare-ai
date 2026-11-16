using AppDelicare.Data.Interface;
using System.Net;

namespace AppDelicare.Data.Repositories
{
    public class Result<T> : IResult<T>
    {
        public T? Data { get; set; }
        public string? ResponseMessage { get; set; }
        public int ResponseCode { get; set; }

        /// <summary>T45R: kết quả thành công (ResponseCode = 200) – dùng cho method nghiệp vụ mới, cùng hình dạng Result cũ.</summary>
        public static Result<T> Success(T? data, string message) => new() { ResponseCode = Convert.ToInt32(HttpStatusCode.OK), ResponseMessage = message, Data = data };

        /// <summary>T45R: lỗi nghiệp vụ (ResponseCode = 400 như mẫu cũ) kèm thông báo tiếng Việt.</summary>
        public static Result<T> Fail(string message, T? data = default) => new() { ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest), ResponseMessage = message, Data = data };
    }
}
