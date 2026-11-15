using AppDelicare.WebApi.Controllers.V1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// T03: chỉ áp dụng cho controller kế thừa <see cref="ApiV1ControllerBase"/>:
    /// khai báo các response lỗi dạng ProblemDetails (để Swagger mô tả đúng). Route cũ không bị đụng.
    /// </summary>
    public sealed class ApiV1Convention : IControllerModelConvention
    {
        private static readonly int[] ProblemStatusCodes =
        [
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status500InternalServerError,
        ];

        public void Apply(ControllerModel controller)
        {
            if (!typeof(ApiV1ControllerBase).IsAssignableFrom(controller.ControllerType))
            {
                return;
            }

            foreach (var action in controller.Actions)
            {
                action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json"));
                foreach (int code in ProblemStatusCodes)
                {
                    action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), code, "application/problem+json"));
                }
            }
        }
    }
}
