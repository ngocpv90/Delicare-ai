using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class StockCount : BaseEntity
    {
        public Guid WarehouseId { get; set; }
        public string? CountNumber { get; set; }
        public DateTime CountDate { get; set; }
        public string? CountType { get; set; }
        public string? Status { get; set; }
        public Guid? ResponsibleEmployeeId { get; set; }
        public string? Note { get; set; }
    }
}
