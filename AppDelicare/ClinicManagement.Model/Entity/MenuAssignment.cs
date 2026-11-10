using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MenuAssignment : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public Guid? ContractId { get; set; }
        public Guid MenuId { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
    }
}
