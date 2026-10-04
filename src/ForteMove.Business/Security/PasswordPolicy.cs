using System.Collections.Generic;
using ForteMove.Models.Common;

namespace ForteMove.Business.Security
{
    public static class PasswordPolicy
    {
        public const int MinimumLength = 15;
        public const int MaximumLength = 128;

        public static void Validate(string password, string field, IList<ValidationError> errors)
        {
            if (errors == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(password) || password.Length < MinimumLength || password.Length > MaximumLength)
            {
                errors.Add(new ValidationError(
                    field,
                    "Password must be between 15 and 128 characters."));
            }
        }
    }
}
