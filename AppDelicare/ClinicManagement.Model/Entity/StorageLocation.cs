using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class StorageLocation : BaseEntity
    {
        public Guid WarehouseId { get; set; }
        public Guid? StorageZoneId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? LocationType { get; set; }
        public string? Status { get; set; }
    }
}
