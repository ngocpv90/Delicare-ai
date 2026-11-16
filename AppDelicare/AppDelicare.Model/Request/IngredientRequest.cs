using System.ComponentModel.DataAnnotations;
using System;

namespace AppDelicare.Model.Request
{
	public class IngredientRequest : BaseRequest
	{
		public Guid? CompanyId { get; set; }
		public Guid? IngredientCategoryId { get; set; }
		public Guid? BaseUnitId { get; set; }
		public string? Code { get; set; }
		public string? Name { get; set; }
		public string? Specification { get; set; }
		public string? StorageCondition { get; set; }
		public bool IsExpiryManaged { get; set; }
		public int? ShelfLifeDays { get; set; }
		public decimal? MinimumStock { get; set; }
		public decimal? SafetyStock { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public Guid? PurchaseUomId { get; set; }
		public Guid? IssueUomId { get; set; }
		[MaxLength(100, ErrorMessage = "StorageType tối đa 100 ký tự.")]
		public string? StorageType { get; set; }
		public decimal? YieldPercent { get; set; }
		public decimal? StandardWastePercent { get; set; }
		public bool? LotControl { get; set; }
		public bool? ExpiryControl { get; set; }
		public bool? QcRequired { get; set; }
	}
}
