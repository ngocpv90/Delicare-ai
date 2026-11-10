using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class StockCountLine : BaseEntity
    {
        public Guid StockCountId { get; set; }
        public Guid IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public decimal SystemQuantity { get; set; }
        public decimal PhysicalQuantity { get; set; }
        public decimal VarianceQuantity { get; set; }
        public Guid UnitId { get; set; }
        public string? VarianceReason { get; set; }
    }
}
