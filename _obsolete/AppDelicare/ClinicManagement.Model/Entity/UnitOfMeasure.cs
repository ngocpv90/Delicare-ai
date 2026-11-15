using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class UnitOfMeasure : BaseEntity
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Symbol { get; set; }
        public string? UnitType { get; set; }
        public string? Status { get; set; }
    }
}
