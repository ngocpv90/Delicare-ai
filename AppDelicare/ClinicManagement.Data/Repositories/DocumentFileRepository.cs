using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;

namespace AppDelicare.Data.Repositories
{
    public class DocumentFileRepository(IUnitOfWork unitOfWork) : Repository<DocumentFile>(unitOfWork), IDocumentFileRepository
    {
    }
}
