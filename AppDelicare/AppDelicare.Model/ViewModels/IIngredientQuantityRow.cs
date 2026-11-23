namespace AppDelicare.Model.ViewModels
{
    /// <summary>MENU-4.7: dòng danh sách chứng từ khi lọc theo nguyên liệu – tổng số lượng + ĐVT của nguyên liệu trên chứng từ (trống khi không lọc).</summary>
    public interface IIngredientQuantityRow
    {
        decimal? IngredientQuantity { get; set; }
        string? IngredientUnitName { get; set; }
    }
}
