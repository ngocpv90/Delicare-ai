using System;

namespace AppDelicare.Model.ViewModels
{
	public class RecipeViewModel : BaseViewModel
	{
		public Guid DishId { get; set; }
		public string? DishName { get; set; }
		public string? VersionCode { get; set; }
		public decimal YieldQuantity { get; set; }
		public Guid? YieldUnitId { get; set; }
		public string? YieldUnitName { get; set; }
		public DateTime EffectiveFrom { get; set; }
		public DateTime? EffectiveTo { get; set; }
		public bool IsActive { get; set; }
		public string? Instructions { get; set; }
		public string? Status { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public int? VersionNo { get; set; }
		public decimal? YieldPercent { get; set; }
		public decimal? StandardOutputQty { get; set; }
		public Guid? ApprovedBy { get; set; }
		public DateTime? ApprovedAt { get; set; }
		public string? Note { get; set; }
		public string? SnapshotJson { get; set; }
		public DateTime? LockedAt { get; set; }
		public Guid? LockedBy { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
