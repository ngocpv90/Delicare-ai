using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class EmployeeRepository(IUnitOfWork unitOfWork) : Repository<Employee>(unitOfWork), IEmployeeRepository
	{
	}
}
