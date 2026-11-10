using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class ContractMeals : BaseEntity
    {
        public Guid ContractId { get; set; }
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public decimal? UnitPrice { get; set; }
        public Guid? UnitId { get; set; }
        public string? Status { get; set; }
    }
}
