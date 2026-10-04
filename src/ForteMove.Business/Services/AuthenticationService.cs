using System;
using ForteMove.Business.Contracts;
using ForteMove.Business.Security;
using ForteMove.Business.Time;
using ForteMove.Models.Security;
using ForteMove.Models.Common;
using System.Collections.Generic;

namespace ForteMove.Business.Services
{
    public sealed class AuthenticationService
    {
        public const int FailureThreshold = 5;
        public const int LockoutMinutes = 15;
        public const string GenericAuthenticationError = "The email address or password is incorrect.";

        private const int MaximumEmailLength = 254;
        private const int MaximumPasswordLength = 128;

        private static readonly byte[] DummySalt = Convert.FromBase64String(
            "9oTbkK0OlDXd1J9sVfD7c5+M9utI8xZSnP+VycNw1X0=");

        private static readonly byte[] DummyHash = Convert.FromBase64String(
            "oYutWvzpS/PsP0xYjVQf2NDy2uYQLRaO9klMmOpnI8E=");

        private readonly IAuthenticationRepository repository;
        private readonly PasswordHasher passwordHasher;
        private readonly IClock clock;

        public AuthenticationService(IAuthenticationRepository repository)
            : this(repository, new PasswordHasher(), new SystemClock())
        {
        }

        public AuthenticationService(
            IAuthenticationRepository repository,
            PasswordHasher passwordHasher,
            IClock clock)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            if (passwordHasher == null)
            {
                throw new ArgumentNullException("passwordHasher");
            }

            if (clock == null)
            {
                throw new ArgumentNullException("clock");
            }

            this.repository = repository;
            this.passwordHasher = passwordHasher;
            this.clock = clock;
        }

        public AuthenticationResult Authenticate(LoginRequest request)
        {
            string password = request == null ? null : request.Password;
            string normalizedEmail = NormalizeEmail(request == null ? null : request.Email);
            string clientIpAddress = NormalizeClientIp(
                request == null ? null : request.ClientIpAddress);
            bool emailLengthIsValid = normalizedEmail.Length <= MaximumEmailLength;
            bool passwordLengthIsValid = password != null && password.Length <= MaximumPasswordLength;
            UserCredentialRecord credential = string.IsNullOrEmpty(normalizedEmail) || !emailLengthIsValid
                ? null
                : repository.FindByNormalizedEmail(normalizedEmail);

            bool passwordMatches;
            if (credential == null || !passwordLengthIsValid)
            {
                passwordHasher.PerformDummyVerification(
                    GetBoundedPasswordCandidate(password),
                    DummySalt,
                    DummyHash,
                    PasswordHasher.DefaultIterations);
                passwordMatches = false;
            }
            else
            {
                passwordMatches = passwordHasher.VerifyPassword(password, credential);
            }

            DateTime now = clock.UtcNow;
            bool isLockedOut = credential != null &&
                credential.LockoutEndUtc.HasValue &&
                credential.LockoutEndUtc.Value > now;

            bool accountCanAuthenticate = credential != null &&
                credential.IsActive &&
                credential.IsRoleActive &&
                credential.Role != RoleCode.Unknown &&
                !isLockedOut;

            if (!passwordMatches || !accountCanAuthenticate)
            {
                if (credential != null && credential.IsActive && credential.IsRoleActive && !isLockedOut)
                {
                    repository.RecordFailedLogin(
                        credential.UserAccountId,
                        now,
                        FailureThreshold,
                        TimeSpan.FromMinutes(LockoutMinutes),
                        clientIpAddress);
                }

                return AuthenticationResult.Failure(GenericAuthenticationError);
            }

            repository.RecordSuccessfulLogin(credential.UserAccountId, now, clientIpAddress);

            PrincipalContext principal = repository.GetPrincipalContext(credential.UserAccountId);
            if (!IsCurrentPrincipalValid(principal))
            {
                return AuthenticationResult.Failure(GenericAuthenticationError);
            }

            return AuthenticationResult.Success(principal);
        }

        public PrincipalContext GetPrincipalContext(long userAccountId)
        {
            if (userAccountId <= 0)
            {
                return null;
            }

            return repository.GetPrincipalContext(userAccountId);
        }

        public void RecordLogout(long userAccountId, string clientIpAddress)
        {
            if (userAccountId <= 0)
            {
                return;
            }

            repository.RecordLogout(userAccountId, clock.UtcNow, NormalizeClientIp(clientIpAddress));
        }

        public ServiceResult<bool> ChangePassword(ChangePasswordRequest request)
        {
            IList<ValidationError> errors = new List<ValidationError>();
            if (request == null || request.UserAccountId <= 0)
            {
                return ServiceResult<bool>.Failure(string.Empty, "A valid authenticated account is required.");
            }

            if (string.IsNullOrEmpty(request.CurrentPassword))
            {
                errors.Add(new ValidationError("CurrentPassword", "Current temporary password is required."));
            }

            PasswordPolicy.Validate(request.NewPassword, "NewPassword", errors);
            if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            {
                errors.Add(new ValidationError("ConfirmPassword", "The new password confirmation does not match."));
            }

            UserCredentialRecord credential = repository.GetCredential(request.UserAccountId);
            if (credential == null || !passwordHasher.VerifyPassword(request.CurrentPassword, credential))
            {
                errors.Add(new ValidationError("CurrentPassword", "The current password is incorrect."));
            }
            else if (passwordHasher.VerifyPassword(request.NewPassword, credential))
            {
                errors.Add(new ValidationError("NewPassword", "Choose a new password that differs from the temporary password."));
            }

            if (errors.Count > 0)
            {
                return ServiceResult<bool>.Failure(errors);
            }

            repository.ChangePassword(
                request.UserAccountId,
                passwordHasher.HashPassword(request.NewPassword),
                clock.UtcNow,
                NormalizeClientIp(request.ClientIpAddress));
            return ServiceResult<bool>.Success(true);
        }

        public static bool IsCurrentPrincipalValid(PrincipalContext principal)
        {
            return principal != null &&
                principal.UserAccountId > 0 &&
                principal.IsActive &&
                principal.IsRoleActive &&
                principal.Role != RoleCode.Unknown;
        }

        private static string NormalizeEmail(string email)
        {
            return string.IsNullOrWhiteSpace(email)
                ? string.Empty
                : email.Trim().ToUpperInvariant();
        }

        private static string NormalizeClientIp(string clientIpAddress)
        {
            if (string.IsNullOrWhiteSpace(clientIpAddress))
            {
                return null;
            }

            string normalized = clientIpAddress.Trim();
            return normalized.Length <= 45 ? normalized : normalized.Substring(0, 45);
        }

        private static string GetBoundedPasswordCandidate(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            return password.Length <= MaximumPasswordLength
                ? password
                : password.Substring(0, MaximumPasswordLength);
        }
    }
}
