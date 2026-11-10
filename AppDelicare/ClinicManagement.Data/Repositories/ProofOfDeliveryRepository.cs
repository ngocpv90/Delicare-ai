using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class ProofOfDeliveryRepository(IUnitOfWork unitOfWork) : Repository<ProofOfDelivery>(unitOfWork), IProofOfDeliveryRepository
	{
	}
}
