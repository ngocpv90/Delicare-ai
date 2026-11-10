using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ProofOfDelivery : BaseEntity
    {
        public Guid DeliveryOrderId { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public decimal? DeliveredQuantity { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public decimal? RejectedQuantity { get; set; }
        public string? SignatureUrl { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Remark { get; set; }
    }
}
