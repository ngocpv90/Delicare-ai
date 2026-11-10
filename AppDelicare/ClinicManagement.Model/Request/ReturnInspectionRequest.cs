using System;

namespace AppDelicare.Model.Request
{
	public class ReturnInspectionRequest : BaseRequest
	{
		public Guid ContainerReturnId { get; set; }
		public DateTime InspectionDate { get; set; }
		public Guid? InspectorEmployeeId { get; set; }
		public string? Result { get; set; }
		public string? Note { get; set; }
	}
}
