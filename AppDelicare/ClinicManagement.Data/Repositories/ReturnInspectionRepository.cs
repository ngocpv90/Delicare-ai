using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ReturnInspectionRepository(IUnitOfWork unitOfWork) : Repository<ReturnInspection>(unitOfWork), IReturnInspectionRepository
	{
	}
}
