using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class IngredientRepository(IUnitOfWork unitOfWork) : Repository<Ingredient>(unitOfWork), IIngredientRepository
	{
	}
}
