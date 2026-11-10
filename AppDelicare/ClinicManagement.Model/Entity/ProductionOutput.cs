using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ProductionOutput : BaseEntity
    {
        public Guid ProductionBatchId { get; set; }
        public Guid DishId { get; set; }
        public decimal ProducedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public Guid? UnitId { get; set; }
        public string? QualityStatus { get; set; }
        public string? Note { get; set; }
    }
}
