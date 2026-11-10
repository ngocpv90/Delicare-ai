using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class InventoryTransaction : BaseEntity
    {
        public Guid WarehouseId { get; set; }
        public Guid IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public Guid? StorageLocationId { get; set; }
        public string? TransactionNumber { get; set; }
        public string? TransactionType { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public Guid UnitId { get; set; }
        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? Reason { get; set; }
    }
}
