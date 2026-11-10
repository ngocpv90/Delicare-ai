using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class StorageLocationRepository(IUnitOfWork unitOfWork) : Repository<StorageLocation>(unitOfWork), IStorageLocationRepository
	{
	}
}
