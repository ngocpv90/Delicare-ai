using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ProductionPlan : BaseEntity
    {
        public string? PlanNumber { get; set; }
        public DateTime PlanDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
