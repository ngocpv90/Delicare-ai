using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class AdministrativeDistrictRepository(IUnitOfWork unitOfWork) : Repository<AdministrativeDistrict>(unitOfWork), IAdministrativeDistrictRepository
    {
	}
}
