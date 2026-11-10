using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MealCost : BaseEntity
    {
        public Guid? OrderId { get; set; }
        public Guid? OrderLineId { get; set; }
        public Guid? DishId { get; set; }
        public decimal AcceptedMealQuantity { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal CostPerMeal { get; set; }
        public decimal? Revenue { get; set; }
        public decimal? GrossMargin { get; set; }
        public DateTime CalculatedAt { get; set; }
    }
}
