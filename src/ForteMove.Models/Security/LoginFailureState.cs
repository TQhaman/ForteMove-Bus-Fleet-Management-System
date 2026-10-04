using System;

namespace ForteMove.Models.Security
{
    public sealed class LoginFailureState
    {
        public int FailedLoginCount { get; set; }

        public DateTime? LockoutEndUtc { get; set; }

        public bool IsLockedOut { get; set; }
    }
}
