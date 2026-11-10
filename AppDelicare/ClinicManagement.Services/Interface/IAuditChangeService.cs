using AppDelicare.Data.Interface;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IAuditChangeService
    {
        public Task<IResults<AuditChangeCustomViewModel>> GetAll(AuditChangeSearchRequest request);
        public Task<IResults<AuditChangeViewModel>> GetAllEntityName();
        public IResults<AuditChangeViewModel> GetAllAction();
        public Task<IResult<bool>> DeleteData();
        public Task<IResults<ColumnNameForeignKeyViewModel>> GetColumnNameForeignKey();
    }
}
