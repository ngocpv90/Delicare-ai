using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R (spec 4.5): quy đổi số lượng giữa 2 đơn vị.</summary>
    public class UomConvertRequest
    {
        public decimal Quantity { get; set; }
        [Required]
        public Guid FromUnitId { get; set; }
        [Required]
        public Guid ToUnitId { get; set; }
        /// <summary>Ngày áp dụng quy đổi (mặc định hôm nay).</summary>
        public DateTime? On { get; set; }
    }

    /// <summary>T45R (spec 5.4): tra đơn giá áp dụng.</summary>
    public class PriceResolveRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        public Guid? ContractId { get; set; }
        public string? MealType { get; set; }
        /// <summary>Ngày áp dụng (mặc định hôm nay).</summary>
        public DateTime? Date { get; set; }
    }
}
