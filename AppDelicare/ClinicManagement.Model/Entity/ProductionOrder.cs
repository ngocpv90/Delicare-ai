using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ProductionOrder : BaseEntity
    {
        public Guid ProductionPlanId { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public DateTime PlannedStartDate { get; set; }
        public DateTime? ActualStartDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? ResponsibleEmployeeId { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
