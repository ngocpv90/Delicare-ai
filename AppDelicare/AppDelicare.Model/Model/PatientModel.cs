using AppDelicare.Model.Entity;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Model.Model
{
    public class PatientModel : Patient
    {
        public List<PatientDetailViewModel>? ListPatientDetail { get; set; }
    }
}
