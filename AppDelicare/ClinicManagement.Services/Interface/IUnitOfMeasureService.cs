using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IUnitOfMeasureService
    {
        public Task<IResults<UnitOfMeasureViewModel>> GetAll(UnitOfMeasureSearchRequest request);
        public Task<IResult<bool>> Save(UnitOfMeasureRequest request, string typeSave);
        public IResults<UnitOfMeasure> GetAllData();
        public Task<IResult<UnitOfMeasure>> GetById(Guid id);
    }
}
