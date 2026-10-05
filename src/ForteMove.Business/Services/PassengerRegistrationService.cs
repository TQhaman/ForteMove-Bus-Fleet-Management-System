using System;
using System.Collections.Generic;
using System.Net.Mail;
using ForteMove.Business.Contracts;
using ForteMove.Business.Exceptions;
using ForteMove.Business.Security;
using ForteMove.Business.Time;
using ForteMove.Models.Common;
using ForteMove.Models.Passengers;

namespace ForteMove.Business.Services
{
    public sealed class PassengerRegistrationService
    {
        private readonly IPassengerRepository repository;
        private readonly PasswordHasher passwordHasher;
        private readonly IClock clock;

        public PassengerRegistrationService(IPassengerRepository repository)
            : this(repository, new PasswordHasher(), new SystemClock()) { }

        public PassengerRegistrationService(IPassengerRepository repository, PasswordHasher passwordHasher, IClock clock)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (passwordHasher == null) throw new ArgumentNullException("passwordHasher");
            if (clock == null) throw new ArgumentNullException("clock");
            this.repository = repository;
            this.passwordHasher = passwordHasher;
            this.clock = clock;
        }

        public ServiceResult<PassengerRegistrationResult> Register(RegisterPassengerRequest request)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null)
                return ServiceResult<PassengerRegistrationResult>.Failure(string.Empty, "Passenger details are required.");

            string firstName = Trim(request.FirstName);
            string lastName = Trim(request.LastName);
            string email = Trim(request.Email);
            string phone = TrimToNull(request.PhoneNumber);

            RequiredLength(firstName, "FirstName", "First name", 100, errors);
            RequiredLength(lastName, "LastName", "Last name", 100, errors);
            if (string.IsNullOrEmpty(email) || email.Length > 254 || !IsEmail(email))
                errors.Add(new ValidationError("Email", "Enter a valid email address."));
            if (phone != null && phone.Length > 30)
                errors.Add(new ValidationError("PhoneNumber", "Phone number cannot exceed 30 characters."));
            PasswordPolicy.Validate(request.Password, "Password", errors);
            if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
                errors.Add(new ValidationError("ConfirmPassword", "The password confirmation does not match."));
            if (errors.Count > 0) return ServiceResult<PassengerRegistrationResult>.Failure(errors);

            PassengerRegistrationAggregate aggregate = new PassengerRegistrationAggregate
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PhoneNumber = phone,
                PasswordHash = passwordHasher.HashPassword(request.Password),
                ClientIpAddress = NormalizeIp(request.ClientIpAddress)
            };

            try
            {
                return ServiceResult<PassengerRegistrationResult>.Success(
                    repository.RegisterPassenger(aggregate, clock.UtcNow));
            }
            catch (PassengerPersistenceException exception)
            {
                return ServiceResult<PassengerRegistrationResult>.Failure(exception.Field, exception.Message);
            }
        }

        private static bool IsEmail(string value)
        {
            try { return string.Equals(new MailAddress(value).Address, value, StringComparison.OrdinalIgnoreCase); }
            catch (FormatException) { return false; }
        }

        private static void RequiredLength(string value, string field, string label, int maximum, IList<ValidationError> errors)
        {
            if (string.IsNullOrEmpty(value)) errors.Add(new ValidationError(field, label + " is required."));
            else if (value.Length > maximum) errors.Add(new ValidationError(field, label + " cannot exceed " + maximum + " characters."));
        }

        private static string Trim(string value) { return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim(); }
        private static string TrimToNull(string value) { string result = Trim(value); return result.Length == 0 ? null : result; }
        private static string NormalizeIp(string value) { string result = TrimToNull(value); return result == null || result.Length <= 45 ? result : result.Substring(0, 45); }
    }
}
