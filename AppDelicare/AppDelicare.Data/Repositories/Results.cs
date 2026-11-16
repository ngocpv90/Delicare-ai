using AppDelicare.Data.Interface;

namespace AppDelicare.Data.Repositories
{
    public class Results<T> : IResults<T>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Results()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Results(List<T> listData, int totalCount)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            ListData = listData;
            TotalRecords = totalCount;
        }

        public long TotalRecords { get; set; }
        public List<T> ListData { get; set; }
        public int ResponseCode { get; set; }
        public string ResponseMessage { get; set; }

        /// <summary>T16: danh sách thành công (ResponseCode = 200, TotalRecords = số dòng) – cùng hình dạng Results cũ.</summary>
        public static Results<T> Success(List<T> listData, string message) => new(listData, listData.Count) { ResponseCode = 200, ResponseMessage = message };

        /// <summary>T16: lỗi nghiệp vụ (ResponseCode = 400 như mẫu cũ), danh sách rỗng.</summary>
        public static Results<T> Fail(string message) => new([], 0) { ResponseCode = 400, ResponseMessage = message };
    }
}
