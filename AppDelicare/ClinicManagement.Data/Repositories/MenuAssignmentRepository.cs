using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MenuAssignmentRepository(IUnitOfWork unitOfWork) : Repository<MenuAssignment>(unitOfWork), IMenuAssignmentRepository
	{
	}
}
