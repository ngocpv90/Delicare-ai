using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ProductionBatch : BaseEntity
    {
        public Guid ProductionOrderId { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? PlannedQuantity { get; set; }
        public decimal? ActualQuantity { get; set; }
        public string? QualityStatus { get; set; }
        public string? Status { get; set; }
    }
}
