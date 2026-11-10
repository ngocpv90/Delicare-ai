using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class CustomerContactRepository(IUnitOfWork unitOfWork) : Repository<CustomerContact>(unitOfWork), ICustomerContactRepository
	{
	}
}
