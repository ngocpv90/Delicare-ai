using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MaterialReservationRepository(IUnitOfWork unitOfWork) : Repository<MaterialReservation>(unitOfWork), IMaterialReservationRepository
	{
	}
}
