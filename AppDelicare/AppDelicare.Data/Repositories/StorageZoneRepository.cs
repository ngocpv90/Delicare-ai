using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class StorageZoneRepository(IUnitOfWork unitOfWork) : Repository<StorageZone>(unitOfWork), IStorageZoneRepository
	{
	}
}
