using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MaterialIssueRepository(IUnitOfWork unitOfWork) : Repository<MaterialIssue>(unitOfWork), IMaterialIssueRepository
	{
	}
}
