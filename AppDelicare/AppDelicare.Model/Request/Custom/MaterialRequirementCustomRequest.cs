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
