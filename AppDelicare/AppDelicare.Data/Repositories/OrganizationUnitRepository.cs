using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class OrganizationUnitRepository(IUnitOfWork unitOfWork) : Repository<OrganizationUnit>(unitOfWork), IOrganizationUnitRepository
	{
	}
}
