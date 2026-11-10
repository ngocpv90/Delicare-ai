using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOpportunityService 
    {
        public Task<IResults<OpportunityViewModel>> GetAll(OpportunitySearchRequest request);
        public Task<IResult<bool>> Save(OpportunityRequest request, string typeSave);
        public IResults<Opportunity> GetAllData();
        public Task<IResult<Opportunity>> GetById(Guid id);
        public IResults<Opportunity> GetAllDataByRegionId(Guid? id);
        public IResults<Opportunity> GetAllDataByAreaId(Guid? id);
        public IResults<Opportunity> GetAllDataByBranchId(Guid? id);
        public IResults<Opportunity> GetAllDataByDepartmentId(Guid? id);
        public IResults<Opportunity> GetAllDataByUserId(Guid? id);
        public IResults<Opportunity> GetAllDataByCustomerId(Guid? id);
    }
}
