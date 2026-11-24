namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.11 (D-89): thực đơn tuần – một thực đơn chung / tuần; buổi → lựa chọn → nhóm món × 7 ngày, mỗi ô một món.</summary>
    public class MenuWeekRequest
    {
        public DateTime? WeekStart { get; set; }
    }

    public class MenuWeekModel
    {
        public DateTime WeekStart { get; set; }
        public Guid? MenuId { get; set; }
        public string? MenuCode { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public bool Editable { get; set; }
        /// <summary>Hạn công bố: thứ Năm trước tuần.</summary>
        public DateTime PublishDeadline { get; set; }
        public List<DateTime> ListDate { get; set; } = [];
        public List<MenuWeekMealModel> ListMeal { get; set; } = [];
        public int EmptyCells { get; set; }
        public int MissingRecipe { get; set; }
        public int FcOver { get; set; }
        public int DuplicateMain { get; set; }
        public decimal? AvgFcPct { get; set; }
        public decimal FcTargetPct { get; set; }
        public int CustomerCount { get; set; }
        /// <summary>Gửi duyệt được khi không còn ô trống / món thiếu công thức / FC vượt (trùng món chỉ cảnh báo).</summary>
        public bool CanSubmit { get; set; }
        public string? SourceNote { get; set; }
    }

    public class MenuWeekMealModel
    {
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        /// <summary>Giá bán bình quân / suất (đơn giá loại suất của các hợp đồng hiệu lực) – mẫu số FC%.</summary>
        public decimal? Price { get; set; }
        public List<MenuWeekOptionModel> ListOption { get; set; } = [];
    }

    public class MenuWeekOptionModel
    {
        public int OptionNo { get; set; }
        public List<decimal> ListCost { get; set; } = [];
        public List<decimal?> ListFcPct { get; set; } = [];
        public List<MenuWeekRowModel> ListRow { get; set; } = [];
    }

    /// <summary>Một nhóm món (DishCategory) × 7 ngày.</summary>
    public class MenuWeekRowModel
    {
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public List<MenuWeekCellModel> ListCell { get; set; } = [];
    }

    public class MenuWeekCellModel
    {
        public int Day { get; set; }
        public Guid? LineId { get; set; }
        public Guid? DishId { get; set; }
        public string? DishName { get; set; }
        public decimal? Cost { get; set; }
        /// <summary>EMPTY / NO_RECIPE / DUPLICATE (món chính trùng trong 7 ngày gồm cuối tuần trước) – null = ổn.</summary>
        public string? Flag { get; set; }
    }

    public class MenuCellRequest
    {
        public Guid MenuId { get; set; }
        public string? MealType { get; set; }
        public int OptionNo { get; set; } = 1;
        public Guid? CategoryId { get; set; }
        public int Day { get; set; }
        /// <summary>null = xóa món của ô.</summary>
        public Guid? DishId { get; set; }
    }

    public class MenuFillRowRequest
    {
        public Guid MenuId { get; set; }
        public string? MealType { get; set; }
        public int OptionNo { get; set; } = 1;
        public Guid? CategoryId { get; set; }
        public Guid DishId { get; set; }
    }

    public class MenuCopyDayRequest
    {
        public Guid MenuId { get; set; }
        public int FromDay { get; set; }
        public int ToDay { get; set; }
    }

    public class MenuSuggestRequest
    {
        public Guid MenuId { get; set; }
        public string? MealType { get; set; }
        public int OptionNo { get; set; } = 1;
        public Guid? CategoryId { get; set; }
        public int Day { get; set; }
        public string? Search { get; set; }
    }

    public class MenuDishSuggestionModel
    {
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public string? CategoryName { get; set; }
        public decimal? Cost { get; set; }
        public decimal? NewFcPct { get; set; }
        public bool HasRecipe { get; set; }
        public int? DaysSinceUsed { get; set; }
    }

    public class MenuAutoDraftResultModel
    {
        public DateTime WeekStart { get; set; }
        public Guid? MenuId { get; set; }
        public string? MenuCode { get; set; }
        public string? SourceCode { get; set; }
        public bool Created { get; set; }
        public string? Message { get; set; }
    }
}
