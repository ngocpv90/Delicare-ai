using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class StorageZone : BaseEntity
    {
        public Guid WarehouseId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? TemperatureRange { get; set; }
        public string? Status { get; set; }
    }
}
