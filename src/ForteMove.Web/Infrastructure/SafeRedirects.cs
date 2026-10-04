using System;
using System.Web;

namespace ForteMove.Web.Infrastructure
{
    public static class SafeRedirects
    {
        public static string GetAdminReturnUrl(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            string value = candidate.Trim();
            if (value.IndexOf('\\') >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0 ||
                value.IndexOf(':') >= 0)
            {
                return null;
            }

            if (value.StartsWith("~/", StringComparison.Ordinal))
            {
                value = VirtualPathUtility.ToAbsolute(value);
            }

            if (!value.StartsWith("/", StringComparison.Ordinal) ||
                value.StartsWith("//", StringComparison.Ordinal))
            {
                return null;
            }

            Uri relativeUri;
            if (!Uri.TryCreate(value, UriKind.Relative, out relativeUri))
            {
                return null;
            }

            int suffixIndex = value.IndexOfAny(new[] { '?', '#' });
            string path = suffixIndex < 0 ? value : value.Substring(0, suffixIndex);
            string adminRoot = VirtualPathUtility.ToAbsolute("~/Admin/");
            return path.StartsWith(adminRoot, StringComparison.OrdinalIgnoreCase)
                ? value
                : null;
        }
    }
}
