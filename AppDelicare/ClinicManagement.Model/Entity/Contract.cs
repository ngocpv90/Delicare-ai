using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class Contract : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public string? ContractNumber { get; set; }
        public string? Name { get; set; }
        public DateTime? SignedDate { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public decimal? ContractValue { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
