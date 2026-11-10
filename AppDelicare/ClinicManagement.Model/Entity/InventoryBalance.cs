using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class InventoryBalance : BaseEntity
    {
        public Guid WarehouseId { get; set; }
        public Guid IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public Guid? StorageLocationId { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public Guid UnitId { get; set; }
        public DateTime LastCalculatedDate { get; set; }
    }
}
