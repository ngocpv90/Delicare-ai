using System;

namespace AppDelicare.Model.Request
{
    public class VehicleRequest : BaseRequest
    {
        public Guid? CompanyId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? Code { get; set; }
        public string? LicensePlate { get; set; }
        public string? Name { get; set; }
        public string? VehicleType { get; set; }
        public string? Brand { get; set; }
        public decimal? LoadCapacityKg { get; set; }
        public decimal? VolumeM3 { get; set; }
        public bool IsRefrigerated { get; set; }
        public Guid? DefaultDriverId { get; set; }
        public DateTime? RegistrationExpiryDate { get; set; }
        public DateTime? InsuranceExpiryDate { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }
}
