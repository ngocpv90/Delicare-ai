using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{

    /// <summary>T13 (spec 6.3): tra thực đơn áp dụng.</summary>
    public class MenuResolveRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        [Required]
        public string? MealType { get; set; }
        [Required]
        public DateTime Date { get; set; }
    }

    /// <summary>T13 (spec 6.2): Approve / Publish / Expire thực đơn (form).</summary>
    public class MenuActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T13 (spec 6.2 clone): sao chép thực đơn thành bản nháp mới (form).</summary>
    public class MenuCloneRequest
    {
        [Required]
        public Guid Id { get; set; }
        [Required, MaxLength(255)]
        public string? Code { get; set; }
        public string? Name { get; set; }
        [Required]
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
