namespace ForteMove.Models.Security
{
    public sealed class ChangePasswordRequest
    {
        public long UserAccountId { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
        public string ClientIpAddress { get; set; }
    }
}
