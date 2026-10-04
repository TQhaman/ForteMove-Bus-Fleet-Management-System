using System.Web;

namespace ForteMove.Web.Infrastructure
{
    public static class ClientIpAddress
    {
        public static string From(HttpRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserHostAddress))
            {
                return null;
            }

            string value = request.UserHostAddress.Trim();
            return value.Length <= 45 ? value : value.Substring(0, 45);
        }
    }
}
