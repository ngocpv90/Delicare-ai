using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R: tạo phiên bản công thức mới (Draft) cho món, có thể sao chép dòng từ phiên bản cũ.</summary>
    public class RecipeVersionRequest
    {
        [Required]
        public Guid DishId { get; set; }
        public Guid? CopyFromRecipeId { get; set; }
    }

    /// <summary>T45R: duyệt / từ chối công thức. Từ chối bắt buộc ghi lý do (Note).</summary>
    public class RecipeDecisionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T45R: khóa công thức đã duyệt, áp dụng từ ngày (mặc định hôm nay).</summary>
    public class RecipeLockRequest
    {
        [Required]
        public Guid Id { get; set; }
        public DateTime? EffectiveFrom { get; set; }
    }
}

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R: tính lại giá thành chuẩn. ListDishId: danh sách Dish.Id cách nhau dấu phẩy, trống = mọi món có công thức đang khóa.</summary>
    public class StandardCostRecalculateRequest
    {
        public string? ListDishId { get; set; }
    }
}
