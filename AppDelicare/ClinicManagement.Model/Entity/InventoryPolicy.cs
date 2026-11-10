using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class InventoryPolicy : BaseEntity
    {
        public Guid IngredientId { get; set; }
        public Guid? WarehouseId { get; set; }
        public decimal? MinimumStock { get; set; }
        public decimal? MaximumStock { get; set; }
        public decimal? SafetyStock { get; set; }
        public bool UseFefo { get; set; }
        public int? ExpiryWarningDays { get; set; }
        public string? Status { get; set; }
    }
}
