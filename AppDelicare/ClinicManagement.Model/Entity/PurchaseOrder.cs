using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class PurchaseOrder : BaseEntity
    {
        public Guid SupplierId { get; set; }
        public Guid? PurchaseRequestId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Status { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Note { get; set; }
    }
}
