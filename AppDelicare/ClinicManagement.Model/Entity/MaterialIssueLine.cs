using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class MaterialIssueLine : BaseEntity
    {
        public Guid MaterialIssueId { get; set; }
        public Guid IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public string? Note { get; set; }
    }
}
