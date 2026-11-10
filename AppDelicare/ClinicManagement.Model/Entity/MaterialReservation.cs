using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MaterialReservation : BaseEntity
    {
        public Guid ProductionOrderId { get; set; }
        public Guid IngredientId { get; set; }
        public decimal ReservedQuantity { get; set; }
        public Guid UnitId { get; set; }
        public string? Status { get; set; }
    }
}
