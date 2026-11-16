using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialLotService
    {
        public Task<IResults<MaterialLotViewModel>> GetAll(MaterialLotSearchRequest request);
        public Task<IResult<bool>> Save(MaterialLotRequest request, string typeSave);
        public IResults<MaterialLot> GetAllData();
        public Task<IResult<MaterialLot>> GetById(Guid id);
    }
}
