using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IShiftService
    {
        public Task<IResults<ShiftViewModel>> GetAll(ShiftSearchRequest request);
        public Task<IResult<bool>> Save(ShiftRequest request, string typeSave);
        public IResults<Shift> GetAllData();
        public Task<IResult<Shift>> GetById(Guid id);
    }
}
