using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class DishRepository(IUnitOfWork unitOfWork) : Repository<Dish>(unitOfWork), IDishRepository
	{
	}
}
