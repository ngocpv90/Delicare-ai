using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class PurchaseRequestLine : BaseEntity
    {
        public Guid PurchaseRequestId { get; set; }
        public Guid IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string? Note { get; set; }
    }
}
