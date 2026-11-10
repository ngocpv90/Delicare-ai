using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class AdministrativeRegionRepository(IUnitOfWork unitOfWork) : Repository<AdministrativeRegion>(unitOfWork), IAdministrativeRegionRepository
    {
	}
}
