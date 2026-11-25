using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IOrganizationUnitService
    {
        public Task<IResults<OrganizationUnitViewModel>> GetAll(OrganizationUnitSearchRequest request);
        public Task<IResult<bool>> Save(OrganizationUnitRequest request, string typeSave);
        public IResults<OrganizationUnit> GetAllData();
        public Task<IResult<OrganizationUnit>> GetById(Guid id);
        // <summary>T45R: cây đơn vị kèm số nhân viên, số kho, tên quản lý.</summary>
        public Task<IResult<List<OrganizationUnitTreeModel>>> GetTree(Guid? companyId);
        // <summary>T45R: đơn vị UnitType = BRANCH.</summary>
        public IResults<OrganizationUnit> GetBranches();
        // UI-6.41 (D-122)
        Task<IResult<List<AppDelicare.Model.Model.OrganizationUnitTreeModel>>> GetOrgTree(Guid? companyId);
        Task<IResult<AppDelicare.Model.Model.OrgSummaryModel>> GetOrgSummary();
        Task<IResult<bool>> Move(AppDelicare.Model.Model.OrgMoveRequest request);
        Task<IResult<bool>> SetStatus(AppDelicare.Model.Model.OrgSetStatusRequest request);
        Task<IResult<bool>> SetManager(AppDelicare.Model.Model.OrgSetManagerRequest request);
    }
}
