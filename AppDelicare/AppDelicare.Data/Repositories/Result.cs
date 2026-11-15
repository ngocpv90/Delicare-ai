using AppDelicare.Data.Interface;

namespace AppDelicare.Data.Repositories
{
    public class Result<T> : IResult<T>
    {
        public T? Data { get; set; }
        public string? ResponseMessage { get; set; }
        public int ResponseCode { get; set; }
    }
}
