using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialIssueService
    {
        public Task<IResults<MaterialIssueViewModel>> GetAll(MaterialIssueSearchRequest request);
        public Task<IResult<bool>> Save(MaterialIssueRequest request, string typeSave);
        public IResults<MaterialIssue> GetAllData();
        public Task<IResult<MaterialIssue>> GetById(Guid id);
    }
}
