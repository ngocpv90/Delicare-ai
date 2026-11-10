using AppDelicare.Model.ViewModels;

namespace AppDelicare.Model.Response
{
    public class UploadFileResponse
    {
        public bool Result { get; set; }
        public List<InformationFileViewModel>? ListFile { get; set; }
    }
}
