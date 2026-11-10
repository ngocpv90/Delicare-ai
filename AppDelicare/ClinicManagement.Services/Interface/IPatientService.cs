using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IPatientService
    {
        public Task<IResults<PatientCustomViewModel>> GetAll(PatientSearchRequest request);
        public Task<IResult<bool>> Save(PatientCustomRequest request, string typeSave);
        public IResults<Patient> GetAllData();
        public Task<IResult<PatientModel>> GetById(Guid id);
        public IResult<long> GenerateCouponCode();
    }
}
