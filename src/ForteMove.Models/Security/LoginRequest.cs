namespace ForteMove.Models.Security
{
    public sealed class LoginRequest
    {
        public string Email { get; set; }

        public string Password { get; set; }

        public string ClientIpAddress { get; set; }
    }
}
