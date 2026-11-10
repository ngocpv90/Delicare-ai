using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class Menu : BaseEntity
    {
        public Guid? CompanyId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? MenuType { get; set; }
        public string? VersionCode { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
