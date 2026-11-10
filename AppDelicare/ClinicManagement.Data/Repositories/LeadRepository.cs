using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class LeadRepository(IUnitOfWork unitOfWork) : Repository<Leads>(unitOfWork), ILeadRepository
    {
    }
}
