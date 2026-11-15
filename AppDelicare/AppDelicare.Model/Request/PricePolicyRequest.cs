using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class PricePolicyRequest : BaseRequest
	{
		public Guid? CustomerId { get; set; }
		public Guid? ContractId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public decimal UnitPrice { get; set; }
		public string? CurrencyCode { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		[MaxLength(100, ErrorMessage = "MealType tối đa 100 ký tự.")]
		public string? MealType { get; set; }
	}
}
