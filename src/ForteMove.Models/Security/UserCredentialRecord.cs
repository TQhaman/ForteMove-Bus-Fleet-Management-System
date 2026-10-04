using System;

namespace ForteMove.Models.Security
{
    public sealed class UserCredentialRecord
    {
        public long UserAccountId { get; set; }

        public string NormalizedEmail { get; set; }

        public string PasswordAlgorithm { get; set; }

        public byte[] PasswordHash { get; set; }

        public byte[] PasswordSalt { get; set; }

        public int PasswordIterations { get; set; }

        public bool IsActive { get; set; }

        public bool IsRoleActive { get; set; }

        public RoleCode Role { get; set; }

        public int FailedLoginCount { get; set; }

        public DateTime? LockoutEndUtc { get; set; }
    }
}
