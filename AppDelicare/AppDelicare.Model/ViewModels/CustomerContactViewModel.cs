using System;

namespace AppDelicare.Model.ViewModels
{
	public class CustomerContactViewModel : BaseViewModel
	{
		public Guid CustomerId { get; set; }
		public string? CustomerName { get; set; }
		public string? FullName { get; set; }
		public string? Position { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
		public bool IsPrimary { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public string? Name { get; set; }
		public string? Status { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
