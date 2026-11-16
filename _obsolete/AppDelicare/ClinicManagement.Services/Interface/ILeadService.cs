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
    public interface ILeadService
    {
        public Task<IResults<LeadViewModel>> GetAll(LeadSearchRequest request);
        public Task<IResult<bool>> Save(LeadRequest request, string typeSave);
        public IResults<Leads> GetAllData();
        public Task<IResult<Leads>> GetById(Guid id);
        public IResults<Leads> GetAllDataByRegionId(Guid? id);
        public IResults<Leads> GetAllDataByAreaId(Guid? id);
        public IResults<Leads> GetAllDataByBranchId(Guid? id);
        public IResults<Leads> GetAllDataByDepartmentId(Guid? id);
        public IResults<Leads> GetAllDataByUserId(Guid? id);
        public IResults<Leads> GetAllDataByCustomerId(Guid? id);
    }
}
