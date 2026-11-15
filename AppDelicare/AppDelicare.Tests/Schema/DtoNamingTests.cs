using AppDelicare.Model.Request;
using FluentAssertions;

namespace AppDelicare.Tests.Schema
{
    /// <summary>
    /// T49 phát hiện khi deploy staging: DTO có 2 thuộc tính chỉ khác hoa / thường (QcInspectionId / QCInspectionId) → trùng tên camelCase
    /// trong JSON (Swagger lỗi, serialize lỗi). Chặn tái phát cho mọi Request / ViewModel / Model.
    /// </summary>
    public class DtoNamingTests
    {
        [Fact]
        public void No_dto_has_properties_differing_only_by_case()
        {
            var types = typeof(BaseRequest).Assembly.GetTypes()
                .Where(t => t.IsClass && t.Namespace is "AppDelicare.Model.Request" or "AppDelicare.Model.Request.Custom" or "AppDelicare.Model.ViewModels"
                    or "AppDelicare.Model.ViewModels.Custom" or "AppDelicare.Model.Model");
            var clashes = types
                .SelectMany(t => t.GetProperties().GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(p => p.Name).Distinct().Count() > 1)
                    .Select(g => $"{t.Name}: {string.Join(" / ", g.Select(p => p.Name).Distinct())}"))
                .ToList();

            clashes.Should().BeEmpty();
        }
    }
}
