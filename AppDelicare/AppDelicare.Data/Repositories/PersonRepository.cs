using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PersonRepository(IUnitOfWork unitOfWork) : Repository<Person>(unitOfWork), IPersonRepository
    {
	}
}
