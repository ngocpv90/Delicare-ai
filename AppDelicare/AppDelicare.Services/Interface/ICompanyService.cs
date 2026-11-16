using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ICompanyService
    {
        public Task<IResults<CompanyViewModel>> GetAll(CompanySearchRequest request);
        public Task<IResult<bool>> Save(CompanyRequest request, string typeSave);
        public IResults<Company> GetAllData();
        public Task<IResult<Company>> GetById(Guid id);
    }
}
