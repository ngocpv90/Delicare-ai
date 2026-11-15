namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// Ngữ cảnh đơn vị từ header <c>X-Branch-Id</c> (= <c>OrganizationUnit.Id</c>, UnitType = BRANCH – Spec 1.2).
    /// Không gửi = tất cả đơn vị được phép. Kiểm tra user có quyền với đơn vị đó: làm ở T07 (phân quyền).
    /// </summary>
    public interface IBranchContext
    {
        Guid? BranchId { get; }
    }

    public sealed class BranchContext(IHttpContextAccessor httpContextAccessor) : IBranchContext
    {
        public const string HeaderName = "X-Branch-Id";
        public const string ItemKey = "BranchId";

        public Guid? BranchId => httpContextAccessor.HttpContext?.Items[ItemKey] as Guid?;
    }

    /// <summary>Đọc + kiểm tra định dạng <c>X-Branch-Id</c> cho route v1; sai GUID → 400 VALIDATION_FAILED.</summary>
    public sealed class BranchHeaderMiddleware(RequestDelegate next)
    {
        public Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(ExceptionToErrorMiddleware.V1Prefix)
                && context.Request.Headers.TryGetValue(BranchContext.HeaderName, out var raw)
                && !string.IsNullOrWhiteSpace(raw))
            {
                if (!Guid.TryParse(raw.ToString(), out Guid branchId) || branchId == Guid.Empty)
                {
                    throw ApiException.Validation("X-Branch-Id", "Header X-Branch-Id phải là GUID của OrganizationUnit.");
                }
                context.Items[BranchContext.ItemKey] = branchId;
            }
            return next(context);
        }
    }
}
