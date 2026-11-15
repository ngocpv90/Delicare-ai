namespace AppDelicare.Model.ViewModels
{
	public class UserClaimViewModel : BaseViewModel
    {
		public Guid? UserId { get; set; }
		public string? UserName { get; set; }
		public Guid? UserGroupId { get; set; }
		public string? UserGroupName { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? CreatedDate { get; set; }
	}
}
