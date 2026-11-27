#nullable enable
namespace AppDelicare.Model.SearchRequest
{
	/// <summary>RETAIL-1: lọc sản phẩm bán lẻ (tìm mã / tên, nhóm, trạng thái).</summary>
	public class RetailProductSearchRequest : BaseSearchRequest
	{
		public string? CategoryCode { get; set; }
		public string? Status { get; set; }
	}
}
