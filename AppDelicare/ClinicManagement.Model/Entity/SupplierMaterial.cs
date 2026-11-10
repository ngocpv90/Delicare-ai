using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class SupplierMaterial : BaseEntity
    {
        public Guid SupplierId { get; set; }
        public Guid IngredientId { get; set; }
        public string? SupplierMaterialCode { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? MinimumOrderQuantity { get; set; }
        public int? LeadTimeDays { get; set; }
        public string? QualityRequirement { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsPreferred { get; set; }
        public string? Status { get; set; }
    }
}
