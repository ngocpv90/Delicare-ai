using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MenuLine : BaseEntity
    {
        public Guid MenuId { get; set; }
        public Guid DishId { get; set; }
        public string? MealType { get; set; }
        public int? DayOfWeek { get; set; }
        public int SortOrder { get; set; }
        public decimal? StandardQuantity { get; set; }
        public string? Note { get; set; }
    }
}
