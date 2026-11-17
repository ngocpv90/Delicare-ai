using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BE3.2 (Catalog 166): nhận lần đo nhiệt độ từ cảm biến IoT.</summary>
    public interface IIotTemperatureService
    {
        public Task<IResult<IotReceiveResultModel>> Receive(List<IotTemperatureItem>? items);
    }
}
