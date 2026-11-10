using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class OrderReconciliation : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid? ProofOfDeliveryId { get; set; }
        public DateTime ReconciliationDate { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal? FinalAmount { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
