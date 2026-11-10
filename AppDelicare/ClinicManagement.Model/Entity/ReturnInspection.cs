using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ReturnInspection : BaseEntity
    {
        public Guid ContainerReturnId { get; set; }
        public DateTime InspectionDate { get; set; }
        public Guid? InspectorEmployeeId { get; set; }
        public string? Result { get; set; }
        public string? Note { get; set; }
    }
}
