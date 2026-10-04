using System;

namespace ForteMove.Models.Security
{
    public sealed class AuthenticationResult
    {
        private AuthenticationResult(bool succeeded, PrincipalContext principal, string errorMessage)
        {
            Succeeded = succeeded;
            Principal = principal;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded { get; private set; }

        public PrincipalContext Principal { get; private set; }

        public string ErrorMessage { get; private set; }

        public static AuthenticationResult Success(PrincipalContext principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException("principal");
            }

            return new AuthenticationResult(true, principal, null);
        }

        public static AuthenticationResult Failure(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("An error message is required.", "errorMessage");
            }

            return new AuthenticationResult(false, null, errorMessage);
        }
    }
}
