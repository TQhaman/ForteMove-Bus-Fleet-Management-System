using System;
using ForteMove.Models.Security;

namespace ForteMove.Business.Contracts
{
    public interface IAuthenticationRepository
    {
        UserCredentialRecord FindByNormalizedEmail(string normalizedEmail);

        PrincipalContext GetPrincipalContext(long userAccountId);

        LoginFailureState RecordFailedLogin(
            long userAccountId,
            DateTime attemptedAtUtc,
            int failureThreshold,
            TimeSpan lockoutDuration,
            string clientIpAddress);

        void RecordSuccessfulLogin(long userAccountId, DateTime loggedInAtUtc, string clientIpAddress);

        void RecordLogout(long userAccountId, DateTime loggedOutAtUtc, string clientIpAddress);
    }
}
