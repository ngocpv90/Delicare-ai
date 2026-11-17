using System;

namespace AppDelicare.Model.SearchRequest
{
	public class ReturnInspectionSearchRequest : BaseSearchRequest
	{
		public Guid? ContainerReturnId { get; set; }
		public DateTime? InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? Result { get; set; }
	}
}
