using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ContainerReturn : BaseEntity
    {
        public Guid? DeliveryOrderId { get; set; }
        public Guid? CustomerId { get; set; }
        public DateTime ReturnDate { get; set; }
        public int ExpectedQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public int DamagedQuantity { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
