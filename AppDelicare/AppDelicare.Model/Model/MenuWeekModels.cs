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
        /// <summary>UI-6.27 (D-108): số món của ngày chưa có giá thành chuẩn – > 0 thì chi phí ngày chưa đủ, không tính FC% (hiện "thiếu giá").</summary>
        public List<int> ListMissingCost { get; set; } = [];
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
        /// <summary>UI-6.13: món đã đổi cho mọi khách (yêu cầu đổi món đã duyệt) – hiện món mới + dấu "đã đổi".</summary>
        public string? ChangedDishName { get; set; }
        public string? ChangeNote { get; set; }
        /// <summary>UI-6.13: có yêu cầu đổi món chờ duyệt cho ô.</summary>
        public bool ChangePending { get; set; }
    }

    /// <summary>UI-6.13 (D-91): đổi món cho mọi khách trên ô thực đơn tuần.</summary>
    public class MenuChangeCellRequest
    {
        public Guid MenuId { get; set; }
        /// <summary>Tuần đang xem (thực đơn có thể hiệu lực nhiều tuần) – ngày ăn = thứ Hai tuần + Day − 1.</summary>
        public DateTime? WeekStart { get; set; }
        public string? MealType { get; set; }
        public int OptionNo { get; set; } = 1;
        public int Day { get; set; }
        public Guid OriginalDishId { get; set; }
        public Guid? RequestedDishId { get; set; }
        /// <summary>SHORTAGE / PRICE / SUPPLIER / QUALITY / OTHER.</summary>
        public string? ReasonCode { get; set; }
        public string? Note { get; set; }
    }

    public class MenuChangeCellPreviewModel
    {
        public DateTime MealDate { get; set; }
        public int Customers { get; set; }
        public int Orders { get; set; }
        public decimal Quantity { get; set; }
        public DateTime CutoffAt { get; set; }
        public bool BeforeCutoff { get; set; }
        public bool SameCategory { get; set; }
        public bool HasRecipe { get; set; }
        public decimal? FcPct { get; set; }
        public bool FcOk { get; set; }
        public bool AutoApprove { get; set; }
        public List<string> ListBlock { get; set; } = [];
    }

    public class MenuChangeWeekItemModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public DateTime MealDate { get; set; }
        public string? OriginalDish { get; set; }
        public string? RequestedDish { get; set; }
        public string? Scope { get; set; }
        public decimal? Quantity { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? RequestedBy { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool AutoApproved { get; set; }
        public Guid? MenuId { get; set; }
        public Guid? OriginalDishId { get; set; }
        public Guid? RequestedDishId { get; set; }
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
        public DateTime? WeekStart { get; set; }
        public string? MealType { get; set; }
        public int OptionNo { get; set; } = 1;
        public Guid? CategoryId { get; set; }
        public int Day { get; set; }
        public string? Search { get; set; }
        /// <summary>UI-6.27 (D-108): mở từ ngày vượt ngưỡng FC → món rẻ hơn cùng nhóm lên trước.</summary>
        public bool CheaperFirst { get; set; }
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

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.13 (D-91 b): khách xin đổi món riêng trên trang đơn.</summary>
    public class OrderDishChangeRequest
    {
        public Guid OrderId { get; set; }
        public Guid? OriginalDishId { get; set; }
        public string? MealType { get; set; }
        public Guid? RequestedDishId { get; set; }
        /// <summary>Số suất của đơn này (trống = cả lựa chọn).</summary>
        public decimal? Quantity { get; set; }
        /// <summary>EMAIL / PHONE / ZALO / DIRECT.</summary>
        public string? Source { get; set; }
        public string? Note { get; set; }
        public DateTime? ApplyFrom { get; set; }
        public DateTime? ApplyTo { get; set; }
        /// <summary>Món thay riêng cho từng đơn trong khoảng ngày (sửa gợi ý).</summary>
        public List<OrderDishOverride> ListOverride { get; set; } = [];
    }

    public class OrderDishOverride
    {
        public Guid OrderId { get; set; }
        public Guid? RequestedDishId { get; set; }
    }

    public class OrderDishModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime MealDate { get; set; }
        public Guid MenuId { get; set; }
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public int OptionNo { get; set; }
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public Guid? CategoryId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class OrderDishChangeModel
    {
        public Guid OrderId { get; set; }
        public List<OrderDishModel> ListDish { get; set; } = [];
        public OrderDishModel? Source { get; set; }
        public List<MenuDishSuggestionModel> ListSuggestion { get; set; } = [];
        public List<OrderDishChangeItemModel> ListItem { get; set; } = [];
    }

    public class OrderDishChangeItemModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime MealDate { get; set; }
        public string? MealName { get; set; }
        public int OptionNo { get; set; }
        public Guid MenuId { get; set; }
        public decimal Quantity { get; set; }
        public Guid? RequestedDishId { get; set; }
        public bool AutoApprove { get; set; }
        public List<string> ListBlock { get; set; } = [];
    }

    public class OrderDishChangeResultModel
    {
        public int AutoApproved { get; set; }
        public int SentToApprove { get; set; }
        public List<string> ListRequestNo { get; set; } = [];
        public List<string> ListError { get; set; } = [];
    }
}

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.11: tải Excel thực đơn tuần (mẫu sheet "1. Menu") – xem trước khớp món / ghi.</summary>
    public class MenuImportModel
    {
        public Guid MenuId { get; set; }
        public int Matched { get; set; }
        public int Unmatched { get; set; }
        public int Written { get; set; }
        public List<MenuImportCellModel> ListCell { get; set; } = [];
    }

    public class MenuImportCellModel
    {
        public string Key { get; set; } = string.Empty;
        public int Row { get; set; }
        public string? MealType { get; set; }
        public int OptionNo { get; set; }
        public int Day { get; set; }
        public string? Category { get; set; }
        public string? Text { get; set; }
        public Guid? DishId { get; set; }
        public string? DishName { get; set; }
        public List<MenuDishSuggestionModel> ListSuggestion { get; set; } = [];
    }
}
