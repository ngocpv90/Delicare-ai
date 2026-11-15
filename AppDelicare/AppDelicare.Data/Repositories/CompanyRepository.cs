using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class CompanyRepository(IUnitOfWork unitOfWork) : Repository<Company>(unitOfWork), ICompanyRepository
	{
	}
}
