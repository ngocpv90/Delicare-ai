using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppDelicare.Model.SearchRequest
{
    public class DepartmentSearchRequest : BaseSearchRequest
    {
        public Guid? BranchId { get; set; }
    }
}
