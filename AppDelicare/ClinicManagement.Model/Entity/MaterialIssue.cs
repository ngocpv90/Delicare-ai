using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MaterialIssue : BaseEntity
    {
        public Guid ProductionOrderId { get; set; }
        public Guid WarehouseId { get; set; }
        public string? IssueNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
