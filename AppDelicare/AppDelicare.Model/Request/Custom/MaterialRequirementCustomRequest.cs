using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// T14 (spec 7.1 calculate, Pilot P5): tính nhu cầu NL cho 1 ngày sản xuất (form, bấm tay thay job 20:30).
    /// <c>WarehouseId</c> trống = mọi kho (của chi nhánh nếu có <c>OrganizationUnitId</c>); <c>OrganizationUnitId</c> = chi nhánh / bếp của kế hoạch.
    /// </summary>
    public class MaterialRequirementCalculateRequest
    {
        [Required]
        public DateTime ProductionDate { get; set; }
        public Guid? WarehouseId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T14: xem kết quả tính nhu cầu đã lưu của 1 ngày sản xuất (+ chi nhánh).</summary>
    public class MaterialRequirementByDateRequest
    {
        [Required]
        public DateTime ProductionDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
    }
}

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// BE2.1 (spec 7.1 create-purchase-request, Catalog 107): lập / làm lại đề nghị mua Nháp từ các dòng thiếu được chọn của kế hoạch, cộng % dự phòng – JSON vì có danh sách.
    /// <c>ListRequirementId</c> trống = mọi dòng thiếu (số mua &gt; 0). Số đề nghị = làm tròn lên 4 số lẻ của số mua × (1 + BufferPercent / 100).
    /// </summary>
    public class MaterialRequirementPurchaseRequest
    {
        [Required]
        public Guid ProductionPlanId { get; set; }
        public List<Guid> ListRequirementId { get; set; } = [];
        /// <summary>% dự phòng 0–100 (mặc định 0).</summary>
        public decimal BufferPercent { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(1000)]
        public string? Note { get; set; }
    }
}
