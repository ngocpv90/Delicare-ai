using System;

namespace AppDelicare.Model.SearchRequest
{
	public class CustomerContactSearchRequest : BaseSearchRequest
	{
		public Guid CustomerId { get; set; }
		public string? FullName { get; set; }
		public string? Phone { get; set; }
		public string? Email { get; set; }
	}
}
