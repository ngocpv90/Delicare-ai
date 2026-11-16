using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class AdministrativeProvinceRepository(IUnitOfWork unitOfWork) : Repository<AdministrativeProvince>(unitOfWork), IAdministrativeProvinceRepository
    {
	}
}
