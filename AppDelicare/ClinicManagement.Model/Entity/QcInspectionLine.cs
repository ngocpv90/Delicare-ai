using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class QcInspectionLine : BaseEntity
    {
        public Guid QcInspectionId { get; set; }
        public Guid? GoodsReceiptLineId { get; set; }
        public string? Criterion { get; set; }
        public string? Result { get; set; }
        public string? Note { get; set; }
    }
}
