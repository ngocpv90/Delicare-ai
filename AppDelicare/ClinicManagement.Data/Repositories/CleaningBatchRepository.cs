using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class CleaningBatchRepository(IUnitOfWork unitOfWork) : Repository<CleaningBatch>(unitOfWork), ICleaningBatchRepository
	{
	}
}
