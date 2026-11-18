using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-2.2: sự việc liên phòng – nhận xử lý / ghi chú / đã xử lý trên tin bot + leo thang.</summary>
    public interface IIncidentService
    {
        Task<IResult<IncidentResultModel>> Act(IncidentActionRequest request);
        Task<IResult<int>> RunEscalation();
    }
}
