namespace AppDelicare.Model.ViewModels
{
    public class UserGroupViewModel : BaseViewModel
    {
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
        // T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
        public DateTime? CreatedDate { get; set; }
    }
}
