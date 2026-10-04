using System;
using System.Collections.Generic;

namespace ForteMove.Models.Drivers
{
    public enum DriverAvailabilityStatus
    {
        Available,
        Unavailable,
        OnLeave,
        Suspended
    }

    public enum DriverLicenceCode
    {
        B,
        EB,
        C1,
        C,
        EC1,
        EC
    }

    public enum CredentialStatus
    {
        Valid,
        ExpiringSoon,
        Expired
    }

    public sealed class CreateDriverRequest
    {
        public string Email { get; set; }
        public string EmployeeNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DriverAvailabilityStatus? AvailabilityStatus { get; set; }
        public string LicenceNumber { get; set; }
        public DriverLicenceCode? LicenceCode { get; set; }
        public DateTime? LicenceExpiryDate { get; set; }
        public string PrdpNumber { get; set; }
        public DateTime? PrdpExpiryDate { get; set; }
        public string TemporaryPassword { get; set; }
    }

    public sealed class UpdateDriverRequest
    {
        public long DriverProfileId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DriverAvailabilityStatus? AvailabilityStatus { get; set; }
        public string LicenceNumber { get; set; }
        public DriverLicenceCode? LicenceCode { get; set; }
        public DateTime? LicenceExpiryDate { get; set; }
        public string PrdpNumber { get; set; }
        public DateTime? PrdpExpiryDate { get; set; }
        public byte[] UserRowVersion { get; set; }
        public byte[] StaffRowVersion { get; set; }
        public byte[] DriverRowVersion { get; set; }
    }

    public class Driver
    {
        public long DriverProfileId { get; set; }
        public long StaffProfileId { get; set; }
        public long UserAccountId { get; set; }
        public string EmployeeNumber { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string EmploymentStatus { get; set; }
        public bool AccountIsActive { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DriverAvailabilityStatus AvailabilityStatus { get; set; }
        public string LicenceNumber { get; set; }
        public DriverLicenceCode LicenceCode { get; set; }
        public DateTime LicenceExpiryDate { get; set; }
        public string PrdpNumber { get; set; }
        public DateTime PrdpExpiryDate { get; set; }
        public CredentialStatus CredentialStatus { get; set; }
        public byte[] UserRowVersion { get; set; }
        public byte[] StaffRowVersion { get; set; }
        public byte[] DriverRowVersion { get; set; }

        public string FullName
        {
            get { return ((FirstName ?? string.Empty) + " " + (LastName ?? string.Empty)).Trim(); }
        }
    }

    public sealed class DriverListItem : Driver
    {
    }

    public sealed class DriverQuery
    {
        public string SearchTerm { get; set; }
        public DriverAvailabilityStatus? AvailabilityStatus { get; set; }
        public CredentialStatus? CredentialStatus { get; set; }
    }

    public sealed class DriverCreationOptions
    {
        public string SuggestedEmployeeNumber { get; set; }
    }

    public sealed class DriverCreationResult
    {
        public long DriverProfileId { get; set; }
        public string EmployeeNumber { get; set; }
    }

    public sealed class DriverPersistenceRecord
    {
        public string Email { get; set; }
        public string NormalizedEmail { get; set; }
        public string EmployeeNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DriverAvailabilityStatus AvailabilityStatus { get; set; }
        public string LicenceNumber { get; set; }
        public DriverLicenceCode LicenceCode { get; set; }
        public DateTime LicenceExpiryDate { get; set; }
        public string PrdpNumber { get; set; }
        public DateTime PrdpExpiryDate { get; set; }
        public string PasswordAlgorithm { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public int PasswordIterations { get; set; }
    }
}
