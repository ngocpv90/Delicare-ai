namespace AppDelicare.Model.SearchRequest
{
	public class UnitMeasureSearchRequest : BaseSearchRequest
    {
		/// <summary>FE-2: lọc trạng thái (uspUnitMeasure_GetAll @Status; trống = tất cả).</summary>
		public string? Status { get; set; }
	}
}
