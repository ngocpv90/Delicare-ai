using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class SupplierRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? TaxCode { get; set; }
		public string? Address { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
		public string? ContactName { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(100, ErrorMessage = "SupplierType tối đa 100 ký tự.")]
		public string? SupplierType { get; set; }
		[MaxLength(100, ErrorMessage = "ContactPhone tối đa 100 ký tự.")]
		public string? ContactPhone { get; set; }
	}
}
