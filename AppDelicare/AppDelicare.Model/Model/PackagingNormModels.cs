namespace AppDelicare.Model.Model
{
    /// <summary>ACC-6 (D-131 j): 1 cột ma trận định mức bao bì = 1 loại suất (món bao bì BB-&lt;buổi&gt;).</summary>
    public sealed class PackagingNormMealModel
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public Guid? DishId { get; set; }
        public string? DishCode { get; set; }
        /// <summary>Bản đang áp dụng (Locked) – số phiên bản, áp dụng từ.</summary>
        public int? ActiveVersion { get; set; }
        public DateTime? ActiveFrom { get; set; }
        /// <summary>Bản đang sửa: Draft / PendingApproval (null = không có).</summary>
        public Guid? WorkRecipeId { get; set; }
        public string? WorkStatus { get; set; }
        public string? WorkStatusName { get; set; }
        public int? WorkVersion { get; set; }
    }

    public sealed class PackagingNormRowModel
    {
        public Guid IngredientId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? UnitName { get; set; }
        /// <summary>Số / 100 suất theo loại suất – bản đang áp dụng.</summary>
        public Dictionary<string, decimal> Active { get; set; } = [];
        /// <summary>Số / 100 suất – bản đang sửa / chờ duyệt (chỉ loại suất có bản đang sửa).</summary>
        public Dictionary<string, decimal> Work { get; set; } = [];
    }

    public sealed class PackagingNormMatrixModel
    {
        public decimal PerPortions { get; set; } = 100;
        public List<PackagingNormMealModel> ListMeal { get; set; } = [];
        public List<PackagingNormRowModel> ListRow { get; set; } = [];
        /// <summary>Phiếu xuất bao bì Nháp đang chờ ghi sổ (mọi ca).</summary>
        public int DraftIssues { get; set; }
        public bool AutoPost { get; set; }
    }

    public sealed class PackagingSupplyModel
    {
        public Guid IngredientId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? UnitName { get; set; }
    }

    public sealed class PackagingNormLineInput
    {
        public Guid IngredientId { get; set; }
        /// <summary>Số dùng cho 100 suất (0 / trống = bỏ dòng).</summary>
        public decimal? PerHundred { get; set; }
    }

    public sealed class PackagingNormSaveRequest
    {
        public string? Meal { get; set; }
        public List<PackagingNormLineInput> ListLine { get; set; } = [];
    }

    public sealed class PackagingNormSubmitRequest
    {
        public string? Meal { get; set; }
        public DateTime? EffectiveFrom { get; set; }
    }

    public sealed class PackagingShortagePrRequest
    {
        public DateTime Day { get; set; }
        public string? ShiftCode { get; set; }
    }
}
