using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    public interface IDashboardService
    {
        public IResult<DashboardModel> GetCount();
        public IResult<DashboardModel> GetListChartOptionsBar();
        public IResult<DashboardModel> GetListChartOptionsPolarArea();
    }
}
