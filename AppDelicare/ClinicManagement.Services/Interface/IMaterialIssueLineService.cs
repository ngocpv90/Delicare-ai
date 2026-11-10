using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialIssueLineService
    {
        public Task<IResults<MaterialIssueLineViewModel>> GetAll(MaterialIssueLineSearchRequest request);
        public Task<IResult<bool>> Save(MaterialIssueLineRequest request, string typeSave);
        public IResults<MaterialIssueLine> GetAllData();
        public Task<IResult<MaterialIssueLine>> GetById(Guid id);
    }
}
