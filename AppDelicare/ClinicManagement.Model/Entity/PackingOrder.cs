using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class PackingOrder : BaseEntity
    {
        public Guid? ProductionBatchId { get; set; }
        public Guid? OrderId { get; set; }
        public string? PackingOrderNumber { get; set; }
        public DateTime PackingDate { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
