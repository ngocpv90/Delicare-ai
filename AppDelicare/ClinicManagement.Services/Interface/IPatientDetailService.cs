using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IPatientDetailService
    {
        public Task<IResults<PatientDetailCustomViewModel>> GetAll(PatientDetailSearchRequest request);
        public Task<IResult<bool>> Save(PatientDetailRequest request, string typeSave);
        public IResults<PatientDetail> GetAllData();
        public Task<IResult<PatientDetail>> GetById(Guid id);
        public Task<IResult<bool>> DeleteByPatientId(Guid? patientId);
        public Task<IResult<bool>> SaveAfterPatient(Guid? patientId, List<PatientDetailRequest> listPatientDetail);
    }
}
