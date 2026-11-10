using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class CustomerRepository(IUnitOfWork unitOfWork) : Repository<Customer>(unitOfWork), ICustomerRepository
	{
	}
}
