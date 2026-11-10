using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class DishCategoryRepository(IUnitOfWork unitOfWork) : Repository<DishCategory>(unitOfWork), IDishCategoryRepository
	{
	}
}
