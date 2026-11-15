using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppDelicare.Model.Entity
{
    /// <summary>
    /// Bảng <c>IntegrationLog</c> (DDL 20261113). Sổ CHỈ GHI THÊM (CLAUDE.md 3.3): không Update / Delete.
    /// Không kế thừa BaseEntity vì bảng không có cột DeletedDate / DeleteddBy.
    /// T03 dùng để lưu kết quả lệnh theo <c>Idempotency-Key</c>. T05 / T06 sẽ sinh lại bằng CodeGen.
    /// </summary>
    [Table("IntegrationLog")]
    public class IntegrationLog
    {
        public Guid Id { get; set; }
        public Guid? IntegrationChannelId { get; set; }

        [MaxLength(20)]
        public string DirectionCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string OperationCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? CorrelationId { get; set; }

        [MaxLength(200)]
        public string? IdempotencyKey { get; set; }

        [MaxLength(128)]
        public string? PayloadHash { get; set; }

        [MaxLength(30)]
        public string LogStatus { get; set; } = string.Empty;

        public int? HttpStatus { get; set; }

        [MaxLength(100)]
        public string? ErrorCode { get; set; }

        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime RequestAt { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? ResponseAt { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime CreatedDate { get; set; }

        public Guid? CreatedBy { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? ModifiedDate { get; set; }

        public Guid? ModifiedBy { get; set; }

        public bool IsDeleted { get; set; }
    }
}
