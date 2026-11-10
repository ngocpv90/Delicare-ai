using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialRequirementService
    {
        public Task<IResults<MaterialRequirementViewModel>> GetAll(MaterialRequirementSearchRequest request);
        public Task<IResult<bool>> Save(MaterialRequirementRequest request, string typeSave);
        public IResults<MaterialRequirement> GetAllData();
        public Task<IResult<MaterialRequirement>> GetById(Guid id);
    }
}
