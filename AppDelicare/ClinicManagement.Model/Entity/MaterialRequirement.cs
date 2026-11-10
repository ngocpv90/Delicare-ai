using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MaterialRequirement : BaseEntity
    {
        public Guid? ProductionPlanId { get; set; }
        public Guid IngredientId { get; set; }
        public decimal RequiredQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal ExpectedIncomingQuantity { get; set; }
        public decimal PurchaseQuantity { get; set; }
        public Guid UnitId { get; set; }
        public string? Status { get; set; }
    }
}
