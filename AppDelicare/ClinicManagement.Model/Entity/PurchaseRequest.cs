using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class PurchaseRequest : BaseEntity
    {
        public string? RequestNumber { get; set; }
        public Guid? RequestedByEmployeeId { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
