using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PatientRepository(IUnitOfWork unitOfWork) : Repository<Patient>(unitOfWork), IPatientRepository
    {
	}
}
