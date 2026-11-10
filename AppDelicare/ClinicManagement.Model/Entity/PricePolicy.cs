using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class PricePolicy : BaseEntity
    {
        public Guid? CustomerId { get; set; }
        public Guid? ContractId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public decimal UnitPrice { get; set; }
        public string? CurrencyCode { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
    }
}
