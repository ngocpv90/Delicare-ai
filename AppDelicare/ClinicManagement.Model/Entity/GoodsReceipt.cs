using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class GoodsReceipt : BaseEntity
    {
        public Guid? PurchaseOrderId { get; set; }
        public Guid SupplierId { get; set; }
        public string? ReceiptNumber { get; set; }
        public DateTime ReceiptDate { get; set; }
        public Guid? WarehouseId { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
