using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMaterialReservationService
    {
        public Task<IResults<MaterialReservationViewModel>> GetAll(MaterialReservationSearchRequest request);
        public Task<IResult<bool>> Save(MaterialReservationRequest request, string typeSave);
        public IResults<MaterialReservation> GetAllData();
        public Task<IResult<MaterialReservation>> GetById(Guid id);
    }
}
