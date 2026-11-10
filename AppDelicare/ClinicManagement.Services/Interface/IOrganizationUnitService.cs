using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrganizationUnitService
    {
        public Task<IResults<OrganizationUnitViewModel>> GetAll(OrganizationUnitSearchRequest request);
        public Task<IResult<bool>> Save(OrganizationUnitRequest request, string typeSave);
        public IResults<OrganizationUnit> GetAllData();
        public Task<IResult<OrganizationUnit>> GetById(Guid id);
    }
}
