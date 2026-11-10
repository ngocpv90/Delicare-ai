using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class AdministrativeUnitRepository(IUnitOfWork unitOfWork) : Repository<AdministrativeUnit>(unitOfWork), IAdministrativeUnitRepository
    {
	}
}
