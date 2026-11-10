using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class LocationTrackingRepository(IUnitOfWork unitOfWork) : Repository<LocationTracking>(unitOfWork), ILocationTrackingRepository
    {
    }
}
