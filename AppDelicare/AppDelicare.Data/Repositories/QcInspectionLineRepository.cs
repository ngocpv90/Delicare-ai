using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class QcInspectionLineRepository(IUnitOfWork unitOfWork) : Repository<QcInspectionLine>(unitOfWork), IQcInspectionLineRepository
	{
	}
}
