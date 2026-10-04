namespace ForteMove.Models.Security
{
    public sealed class PrincipalContext
    {
        public long UserAccountId { get; set; }

        public string Email { get; set; }

        public string DisplayName { get; set; }

        public RoleCode Role { get; set; }

        public string RoleDisplayName { get; set; }

        public bool IsActive { get; set; }

        public bool IsRoleActive { get; set; }

        public bool MustChangePassword { get; set; }
    }
}
