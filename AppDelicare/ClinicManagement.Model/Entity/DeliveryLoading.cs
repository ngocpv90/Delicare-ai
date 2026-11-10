using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class DeliveryLoading : BaseEntity
    {
        public Guid DeliveryOrderId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? VehicleCode { get; set; }
        public DateTime LoadingDate { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
