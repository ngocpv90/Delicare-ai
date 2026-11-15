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
    }
}
