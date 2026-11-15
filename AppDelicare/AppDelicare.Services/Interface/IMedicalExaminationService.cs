using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IMedicalExaminationService
    {
        public Task<IResults<MedicalExaminationViewModel>> GetAll(MedicalExaminationSearchRequest request);
        public Task<IResult<bool>> Save(MedicalExaminationRequest request, string typeSave);
        public IResults<MedicalExamination> GetAllData();
        public Task<IResult<MedicalExamination>> GetById(Guid id);
    }
}
