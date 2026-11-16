using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MaterialIssueLineRepository(IUnitOfWork unitOfWork) : Repository<MaterialIssueLine>(unitOfWork), IMaterialIssueLineRepository
	{
	}
}
