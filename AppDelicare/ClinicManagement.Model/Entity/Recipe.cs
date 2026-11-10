using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class Recipe : BaseEntity
    {
        public Guid DishId { get; set; }
        public string? VersionCode { get; set; }
        public decimal YieldQuantity { get; set; }
        public Guid? YieldUnitId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public string? Instructions { get; set; }
        public string? Status { get; set; }
    }
}
