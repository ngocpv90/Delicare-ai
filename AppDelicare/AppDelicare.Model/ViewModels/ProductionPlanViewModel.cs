using System;

namespace AppDelicare.Model.ViewModels
{
	public class ProductionPlanViewModel : BaseViewModel
	{
		public string? PlanNumber { get; set; }
		public DateTime PlanDate { get; set; }
		public Guid? OrganizationUnitId { get; set; }
		public string? OrganizationUnitName { get; set; }
		public string? Status { get; set; }
		public string? Note { get; set; }
		// T45R: cột bổ sung theo DDL 124 bảng (tools/AppDelicare.CodeGen --merge).
		public DateTime? ProductionDate { get; set; }
		public Guid? ShiftId { get; set; }
		public string? ShiftName { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
