using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
	public class MedicalExaminationRepository(IUnitOfWork unitOfWork) : Repository<MedicalExamination>(unitOfWork), IMedicalExaminationRepository
    {
	}
}
