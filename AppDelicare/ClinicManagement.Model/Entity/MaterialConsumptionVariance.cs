using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MaterialConsumptionVariance : BaseEntity
    {
        public Guid ProductionOrderId { get; set; }
        public Guid IngredientId { get; set; }
        public decimal StandardQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal VarianceQuantity { get; set; }
        public decimal? VarianceCost { get; set; }
        public string? Reason { get; set; }
    }
}
