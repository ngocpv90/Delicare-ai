using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class DeliveryOrder : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid? CustomerDeliveryLocationId { get; set; }
        public Guid? DeliveryRouteId { get; set; }
        public string? DeliveryNumber { get; set; }
        public DateTime PlannedDeliveryTime { get; set; }
        public DateTime? ActualDeliveryTime { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
