using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MaterialLot : BaseEntity
    {
        public Guid IngredientId { get; set; }
        public Guid? SupplierId { get; set; }
        public Guid? GoodsReceiptLineId { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string? QualityStatus { get; set; }
        public string? Status { get; set; }
    }
}
