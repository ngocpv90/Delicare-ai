using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MaterialRequirementRepository(IUnitOfWork unitOfWork) : Repository<MaterialRequirement>(unitOfWork), IMaterialRequirementRepository
	{
	}
}
