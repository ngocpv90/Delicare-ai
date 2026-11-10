using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class CleaningBatch : BaseEntity
    {
        public string? CleaningBatchNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int ContainerQuantity { get; set; }
        public Guid? ResponsibleEmployeeId { get; set; }
        public string? QualityStatus { get; set; }
        public string? Status { get; set; }
    }
}
