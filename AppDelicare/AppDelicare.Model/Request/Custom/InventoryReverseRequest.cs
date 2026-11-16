using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R: đảo một bút toán kho (T08).</summary>
    public class InventoryReverseRequest
    {
        /// <summary>Id bút toán gốc.</summary>
        [Required]
        public Guid Id { get; set; }
        /// <summary>Lý do đảo (3–1000 ký tự), lưu ở cột Reason của bút toán đảo.</summary>
        public string? Reason { get; set; }
    }
}
