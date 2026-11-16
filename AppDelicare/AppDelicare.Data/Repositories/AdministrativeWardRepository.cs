using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class AdministrativeWardRepository(IUnitOfWork unitOfWork) : Repository<AdministrativeWard>(unitOfWork), IAdministrativeWardRepository
    {
	}
}
