using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class RegionRepository(IUnitOfWork unitOfWork) : Repository<Region>(unitOfWork), IRegionRepository
    {
    }
}
