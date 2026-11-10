using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class PatientDetailRepository(IUnitOfWork unitOfWork) : Repository<PatientDetail>(unitOfWork), IPatientDetailRepository
    {
	}
}
