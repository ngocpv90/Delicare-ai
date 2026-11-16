using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPositionService
    {
        public Task<IResults<PositionViewModel>> GetAll(PositionSearchRequest request);
        public Task<IResult<bool>> Save(PositionRequest request, string typeSave);
        public IResults<Position> GetAllData();
        public Task<IResult<Position>> GetById(Guid id);
    }
}
