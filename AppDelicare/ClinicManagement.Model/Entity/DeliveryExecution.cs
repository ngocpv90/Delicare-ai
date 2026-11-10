using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class DeliveryExecution : BaseEntity
    {
        public Guid DeliveryOrderId { get; set; }
        public DateTime? DepartedAt { get; set; }
        public DateTime? ArrivedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public string? DriverName { get; set; }
        public string? VehicleCode { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
