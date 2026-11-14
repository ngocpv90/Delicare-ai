
namespace AppDelicare.Model.Entity
{
    public class Employee : BaseEntity
    {
        public string? Code { get; set; }
        public Guid? UserId { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? IdentityNumber { get; set; }
        public string? Title { get; set; }
        public string? JobPosition { get; set; }
        public string? EmployeeStatus { get; set; }
        // Ngay vao lam
        public DateTime? JoinDate { get; set; }
        //Ngay nghi viec
        public DateTime? TerminationDate { get; set; }
        public string? WorkEmail { get; set; }
        public string? WorkPhone { get; set; }
        public string? MobilePhone { get; set; }
        public Guid? RegionId { get; set; }
        public Guid? AreaId { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? DirectManagerId { get; set; }
        public string? ExternalValue { get; set; }
        public string? ExternalData { get; set; }
        public string? Note { get; set; }
        // Truong phuc vu van hanh (Master Data - Employee)
        public Guid? CompanyId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? PositionId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public DateTime? HireDate { get; set; }
        public string? Status { get; set; }
    }
}
