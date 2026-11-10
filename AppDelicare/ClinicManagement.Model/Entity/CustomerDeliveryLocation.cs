using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class CustomerDeliveryLocation : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? DeliveryInstructions { get; set; }
        public string? Status { get; set; }
    }
}
