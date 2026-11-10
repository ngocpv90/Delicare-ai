using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class AuditChangeRepository(IUnitOfWork unitOfWork) : Repository<AuditChange>(unitOfWork), IAuditChangeRepository
    {
    }
}
