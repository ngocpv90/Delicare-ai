using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class StandardCost : BaseEntity
    {
        public Guid DishId { get; set; }
        public Guid? RecipeId { get; set; }
        public decimal CostPerServing { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? CurrencyCode { get; set; }
        public string? Status { get; set; }
    }
}
