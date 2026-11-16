using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>T45B.5: trạng thái (StatusCatalog sinh từ STATUS_CATALOG.md) và danh mục tham chiếu (appsettings ReferenceLists). Không đọc DB.</summary>
    public class MetaDataService(IOptions<ReferenceListsOptions> referenceLists) : IMetaDataService
    {
        private readonly ReferenceListsOptions _referenceLists = referenceLists.Value;

        public IResults<StatusSetModel> GetStatuses(string? key)
        {
            try
            {
                var list = StatusCatalog.All
                    .Where(x => string.IsNullOrWhiteSpace(key) || x.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || x.Table.Equals(key, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new StatusSetModel
                    {
                        Key = x.Key,
                        Table = x.Table,
                        Column = x.Column,
                        Source = x.Source,
                        ListItem = x.Codes.Select((c, i) => new StatusItemModel { Code = c, Label = i < x.Labels.Length ? x.Labels[i] : c }).ToList(),
                    }).ToList();
                return new Results<StatusSetModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStatuses: ", ex.ToString()));
                throw;
            }
        }

        public IResults<ReferenceListModel> GetReferenceLists(string? name)
        {
            try
            {
                var list = _referenceLists.Lists
                    .Where(x => string.IsNullOrWhiteSpace(name) || x.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Key)
                    .Select(x => new ReferenceListModel
                    {
                        Name = x.Key,
                        ListItem = x.Value.Select(v => new StatusItemModel { Code = v.Code, Label = v.Label }).ToList(),
                    }).ToList();
                return new Results<ReferenceListModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetReferenceLists: ", ex.ToString()));
                throw;
            }
        }
    }
}
