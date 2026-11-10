using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class Forecast : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public DateTime ForecastDate { get; set; }
        public string? MealType { get; set; }
        public decimal ForecastQuantity { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
