using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ContainerReturnRepository(IUnitOfWork unitOfWork) : Repository<ContainerReturn>(unitOfWork), IContainerReturnRepository
	{
	}
}
