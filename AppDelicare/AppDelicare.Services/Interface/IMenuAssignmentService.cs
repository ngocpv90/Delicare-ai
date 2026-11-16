using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMenuAssignmentService
    {
        public Task<IResults<MenuAssignmentViewModel>> GetAll(MenuAssignmentSearchRequest request);
        public Task<IResult<bool>> Save(MenuAssignmentRequest request, string typeSave);
        public IResults<MenuAssignment> GetAllData();
        public Task<IResult<MenuAssignment>> GetById(Guid id);
    }
}
