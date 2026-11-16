namespace AppDelicare.Data.Share
{
    /// <summary>T08: cố sửa / xóa (kể cả xóa mềm) bản ghi của sổ chỉ ghi thêm (CLAUDE.md 3.3). Muốn sửa: ghi bút toán đảo / điều chỉnh.</summary>
    public sealed class AppendOnlyViolationException(string entityName)
        : InvalidOperationException($"Sổ '{entityName}' chỉ ghi thêm: không được sửa / xóa. Hãy ghi bút toán đảo hoặc điều chỉnh tham chiếu dòng gốc.")
    {
        public string EntityName { get; } = entityName;
    }

    /// <summary>
    /// Vi phạm quy tắc nghiệp vụ từ tầng service (không phụ thuộc WebApi). Route v1 đổi thành envelope với
    /// <see cref="StatusCode"/> + <see cref="Code"/> (Phụ lục A, vd. 422 INSUFFICIENT_STOCK, 409 INVALID_TRANSITION, 404 NOT_FOUND).
    /// </summary>
    public sealed class BusinessRuleException(int statusCode, string code, string message, string? field = null) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
        public string Code { get; } = code;
        public string? Field { get; } = field;
    }
}
