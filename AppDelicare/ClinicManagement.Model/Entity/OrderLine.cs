using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class OrderLine : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid? ContractMealId { get; set; }
        public Guid? MenuId { get; set; }
        public string? MealType { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public string? Note { get; set; }
    }
}
