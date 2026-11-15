namespace AppDelicare.Model.Entity
{
    public class BaseEntity
    {
        public Guid Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public Guid? ModifiedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
        /// <summary>DEPRECATED (lỗi chính tả) – vẫn ghi song song với <see cref="DeletedBy"/>.</summary>
        public Guid? DeleteddBy { get; set; }
        /// <summary>Người xóa mềm (cột mới, DEPRECATED_COLUMNS.md). Bảng không có cột này: EF Ignore.</summary>
        public Guid? DeletedBy { get; set; }
        public bool IsDeleted { get; set; }
    }
}
