using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class CustomerContactRequest : BaseRequest
	{
		public Guid CustomerId { get; set; }
		public string? FullName { get; set; }
		public string? Position { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
		public bool IsPrimary { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(500, ErrorMessage = "Name tối đa 500 ký tự.")]
		public string? Name { get; set; }
		[MaxLength(50, ErrorMessage = "Status tối đa 50 ký tự.")]
		public string? Status { get; set; }
	}
}
