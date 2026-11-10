using AppDelicare.Data.Interface;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface ILocationTrackingService
    {
        public Task<IResults<LocationTrackingCustomViewModel>> GetAll(LocationTrackingSearchRequest request);
        public IResult<bool> DeleteData();
    }
}
