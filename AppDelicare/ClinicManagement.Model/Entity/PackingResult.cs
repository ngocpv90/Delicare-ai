using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class PackingResult : BaseEntity
    {
        public Guid PackingOrderId { get; set; }
        public Guid? ProductionOutputId { get; set; }
        public decimal PackedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public string? PackagingType { get; set; }
        public string? QualityStatus { get; set; }
        public string? Note { get; set; }
    }
}
