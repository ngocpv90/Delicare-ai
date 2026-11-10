using AppDelicare.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.Entity
{
    public class RecipeLine : BaseEntity
    {
        public Guid RecipeId { get; set; }
        public Guid IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public decimal? ExpectedWasteRate { get; set; }
        public string? PreparationNote { get; set; }
        public int SortOrder { get; set; }
    }
}
