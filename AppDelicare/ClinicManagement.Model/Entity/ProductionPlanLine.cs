using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ProductionPlanLine : BaseEntity
    {
        public Guid ProductionPlanId { get; set; }
        public Guid? OrderLineId { get; set; }
        public Guid DishId { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal? ActualQuantity { get; set; }
        public Guid? ShiftId { get; set; }
        public string? Status { get; set; }
    }
}
