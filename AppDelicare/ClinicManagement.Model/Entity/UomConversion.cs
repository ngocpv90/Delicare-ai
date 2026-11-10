using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class UomConversion : BaseEntity
    {
        public Guid FromUnitId { get; set; }
        public Guid ToUnitId { get; set; }
        public decimal ConversionFactor { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public string? Note { get; set; }
    }
}
