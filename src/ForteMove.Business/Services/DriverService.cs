using System;
using System.Collections.Generic;
using System.Linq;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Identifiers;
using ForteMove.Business.Security;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Drivers;
using ForteMove.Models.Security;

namespace ForteMove.Business.Services
{
    public sealed class DriverService
    {
        private readonly IDriverRepository repository;
        private readonly PasswordHasher hasher;
        private readonly IClock clock;

        public DriverService(IDriverRepository repository)
            : this(repository, new PasswordHasher(), new SystemClock()) { }

        public DriverService(IDriverRepository repository, PasswordHasher hasher, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (hasher == null) throw new ArgumentNullException("hasher");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository = repository;
            this.hasher = hasher;
            this.clock = clock;
        }

        public DriverCreationOptions GetCreationOptions()
        {
            return new DriverCreationOptions
            {
                SuggestedEmployeeNumber = IdentifierCodePolicy.FormatDriverEmployeeNumber(repository.GetNextEmployeeNumberSequence())
            };
        }

        public ServiceResult<DriverCreationResult> CreateDriver(CreateDriverRequest request, long actorUserAccountId)
        {
            IList<ValidationError> errors = Validate(request);
            PasswordPolicy.Validate(request == null ? null : request.TemporaryPassword, "TemporaryPassword", errors);
            if (actorUserAccountId <= 0) errors.Add(new ValidationError(string.Empty, "A valid administrator is required."));
            if (errors.Count > 0) return ServiceResult<DriverCreationResult>.Failure(errors);

            PasswordHash password = hasher.HashPassword(request.TemporaryPassword);
            DriverPersistenceRecord record = new DriverPersistenceRecord
            {
                Email = request.Email.Trim(),
                NormalizedEmail = request.Email.Trim().ToUpperInvariant(),
                EmployeeNumber = NormalizeIdentifier(request.EmployeeNumber),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PhoneNumber = NormalizeOptional(request.PhoneNumber),
                DateOfBirth = request.DateOfBirth.Value.Date,
                AvailabilityStatus = request.AvailabilityStatus.Value,
                LicenceNumber = NormalizeIdentifier(request.LicenceNumber),
                LicenceCode = request.LicenceCode.Value,
                LicenceExpiryDate = request.LicenceExpiryDate.Value.Date,
                PrdpNumber = NormalizeIdentifier(request.PrdpNumber),
                PrdpExpiryDate = request.PrdpExpiryDate.Value.Date,
                PasswordAlgorithm = password.Algorithm,
                PasswordHash = password.Hash,
                PasswordSalt = password.Salt,
                PasswordIterations = password.Iterations
            };
            try
            {
                long id = repository.CreateDriver(record, actorUserAccountId);
                IList<string> warnings = GetCredentialWarnings(record.LicenceExpiryDate, record.PrdpExpiryDate);
                return ServiceResult<DriverCreationResult>.Success(new DriverCreationResult
                {
                    DriverProfileId = id,
                    EmployeeNumber = record.EmployeeNumber
                }, warnings);
            }
            catch (DriverPersistenceException exception)
            {
                return ServiceResult<DriverCreationResult>.Failure(exception.Field, exception.Message);
            }
        }

        public IList<DriverListItem> GetDrivers(DriverQuery query)
        {
            IEnumerable<Driver> rows = repository.GetDrivers() ?? new List<Driver>();
            string search = NormalizeOptional(query == null ? null : query.SearchTerm);
            if (search != null)
            {
                rows = rows.Where(d => Contains(d.EmployeeNumber, search) || Contains(d.FullName, search) || Contains(d.Email, search)
                    || Contains(d.LicenceNumber, search) || Contains(d.PrdpNumber, search));
            }
            if (query != null && query.AvailabilityStatus.HasValue)
                rows = rows.Where(d => d.AvailabilityStatus == query.AvailabilityStatus.Value);
            foreach (Driver d in rows) d.CredentialStatus = GetCredentialStatus(d.LicenceExpiryDate, d.PrdpExpiryDate);
            if (query != null && query.CredentialStatus.HasValue)
                rows = rows.Where(d => d.CredentialStatus == query.CredentialStatus.Value);
            return rows.Select(ToListItem).OrderBy(d => d.EmployeeNumber, StringComparer.Ordinal).ToList();
        }

        public Driver GetDriver(long driverProfileId)
        {
            Driver driver = repository.GetDriver(driverProfileId);
            if (driver != null) driver.CredentialStatus = GetCredentialStatus(driver.LicenceExpiryDate, driver.PrdpExpiryDate);
            return driver;
        }

        public Driver GetDriverByUserAccount(long userAccountId)
        {
            Driver driver = repository.GetDriverByUserAccount(userAccountId);
            if (driver != null) driver.CredentialStatus = GetCredentialStatus(driver.LicenceExpiryDate, driver.PrdpExpiryDate);
            return driver;
        }

        public ServiceResult<bool> UpdateDriver(UpdateDriverRequest request, long actorUserAccountId)
        {
            IList<ValidationError> errors = Validate(request);
            if (request == null || request.DriverProfileId <= 0) errors.Add(new ValidationError(string.Empty, "A valid Driver is required."));
            if (request != null && (request.UserRowVersion == null || request.StaffRowVersion == null || request.DriverRowVersion == null))
                errors.Add(new ValidationError(string.Empty, "The Driver record must be refreshed before saving."));
            if (errors.Count > 0) return ServiceResult<bool>.Failure(errors);
            Normalize(request);
            try
            {
                repository.UpdateDriver(request, actorUserAccountId);
                return ServiceResult<bool>.Success(true, GetCredentialWarnings(request.LicenceExpiryDate.Value, request.PrdpExpiryDate.Value));
            }
            catch (DriverPersistenceException exception)
            {
                return ServiceResult<bool>.Failure(exception.Field, exception.Message);
            }
        }

        public CredentialStatus GetCredentialStatus(DateTime licenceExpiry, DateTime prdpExpiry)
        {
            DateTime today = clock.Today.Date;
            DateTime earliest = licenceExpiry.Date < prdpExpiry.Date ? licenceExpiry.Date : prdpExpiry.Date;
            if (earliest < today) return CredentialStatus.Expired;
            return earliest <= today.AddDays(30) ? CredentialStatus.ExpiringSoon : CredentialStatus.Valid;
        }

        private IList<ValidationError> Validate(CreateDriverRequest request)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null) { errors.Add(new ValidationError(string.Empty, "Driver details are required.")); return errors; }
            ValidateCommon(errors, request.Email, request.FirstName, request.LastName, request.DateOfBirth,
                request.AvailabilityStatus, request.LicenceNumber, request.LicenceCode, request.LicenceExpiryDate,
                request.PrdpNumber, request.PrdpExpiryDate);
            if (string.IsNullOrWhiteSpace(request.EmployeeNumber) || request.EmployeeNumber.Trim().Length > 50)
                errors.Add(new ValidationError("EmployeeNumber", "A valid employee number is required."));
            return errors;
        }

        private IList<ValidationError> Validate(UpdateDriverRequest request)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null) { errors.Add(new ValidationError(string.Empty, "Driver details are required.")); return errors; }
            ValidateCommon(errors, request.Email, request.FirstName, request.LastName, request.DateOfBirth,
                request.AvailabilityStatus, request.LicenceNumber, request.LicenceCode, request.LicenceExpiryDate,
                request.PrdpNumber, request.PrdpExpiryDate);
            return errors;
        }

        private void ValidateCommon(IList<ValidationError> errors, string email, string firstName, string lastName,
            DateTime? dateOfBirth, DriverAvailabilityStatus? availability, string licenceNumber,
            DriverLicenceCode? licenceCode, DateTime? licenceExpiry, string prdpNumber, DateTime? prdpExpiry)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 254 || email.IndexOf('@') <= 0)
                errors.Add(new ValidationError("Email", "Enter a valid email address."));
            ValidateText(errors, "FirstName", "First name", firstName, 100);
            ValidateText(errors, "LastName", "Last name", lastName, 100);
            if (!dateOfBirth.HasValue) errors.Add(new ValidationError("DateOfBirth", "Date of birth is required."));
            else if (dateOfBirth.Value.Date.AddYears(21) > clock.Today.Date)
                errors.Add(new ValidationError("DateOfBirth", "A Driver must be at least 21 years old."));
            if (!availability.HasValue || !Enum.IsDefined(typeof(DriverAvailabilityStatus), availability.Value))
                errors.Add(new ValidationError("AvailabilityStatus", "Select a valid availability status."));
            ValidateText(errors, "LicenceNumber", "Licence number", licenceNumber, 50);
            if (!licenceCode.HasValue || !Enum.IsDefined(typeof(DriverLicenceCode), licenceCode.Value))
                errors.Add(new ValidationError("LicenceCode", "Select a valid licence code."));
            if (!licenceExpiry.HasValue) errors.Add(new ValidationError("LicenceExpiryDate", "Licence expiry date is required."));
            ValidateText(errors, "PrdpNumber", "PrDP number", prdpNumber, 50);
            if (!prdpExpiry.HasValue) errors.Add(new ValidationError("PrdpExpiryDate", "PrDP expiry date is required."));
        }

        private IList<string> GetCredentialWarnings(DateTime licence, DateTime prdp)
        {
            IList<string> warnings = new List<string>();
            if (licence.Date < clock.Today.Date) warnings.Add("The Driver licence is expired; this Driver cannot be assigned until it is renewed.");
            if (prdp.Date < clock.Today.Date) warnings.Add("The PrDP is expired; this Driver cannot be assigned until it is renewed.");
            return warnings;
        }

        private static void Normalize(UpdateDriverRequest request)
        {
            request.Email = request.Email.Trim(); request.FirstName = request.FirstName.Trim(); request.LastName = request.LastName.Trim();
            request.PhoneNumber = NormalizeOptional(request.PhoneNumber); request.LicenceNumber = NormalizeIdentifier(request.LicenceNumber);
            request.PrdpNumber = NormalizeIdentifier(request.PrdpNumber); request.DateOfBirth = request.DateOfBirth.Value.Date;
            request.LicenceExpiryDate = request.LicenceExpiryDate.Value.Date; request.PrdpExpiryDate = request.PrdpExpiryDate.Value.Date;
        }

        private static void ValidateText(IList<ValidationError> errors, string field, string label, string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add(new ValidationError(field, label + " is required."));
            else if (value.Trim().Length > max) errors.Add(new ValidationError(field, label + " is too long."));
        }
        private static string NormalizeIdentifier(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant(); }
        private static string NormalizeOptional(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
        private static bool Contains(string value, string search) { return value != null && value.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0; }
        private static DriverListItem ToListItem(Driver d)
        {
            return new DriverListItem { DriverProfileId=d.DriverProfileId, StaffProfileId=d.StaffProfileId, UserAccountId=d.UserAccountId,
                EmployeeNumber=d.EmployeeNumber, Email=d.Email, FirstName=d.FirstName, LastName=d.LastName, PhoneNumber=d.PhoneNumber,
                EmploymentStatus=d.EmploymentStatus, AccountIsActive=d.AccountIsActive, DateOfBirth=d.DateOfBirth,
                AvailabilityStatus=d.AvailabilityStatus, LicenceNumber=d.LicenceNumber, LicenceCode=d.LicenceCode,
                LicenceExpiryDate=d.LicenceExpiryDate, PrdpNumber=d.PrdpNumber, PrdpExpiryDate=d.PrdpExpiryDate,
                CredentialStatus=d.CredentialStatus, UserRowVersion=d.UserRowVersion, StaffRowVersion=d.StaffRowVersion, DriverRowVersion=d.DriverRowVersion };
        }
    }
}
