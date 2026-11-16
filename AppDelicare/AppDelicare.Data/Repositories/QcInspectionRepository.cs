using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class QcInspectionRepository(IUnitOfWork unitOfWork) : Repository<QcInspection>(unitOfWork), IQcInspectionRepository
	{
	}
}
