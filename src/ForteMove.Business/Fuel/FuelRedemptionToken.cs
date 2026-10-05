using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
namespace ForteMove.Business.Fuel
{
    public static class FuelRedemptionToken
    {
        public static string Create()
        {
            var bytes=new byte[32];
            using(var random=RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return "FMFV1."+Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
        }
        public static byte[] Hash(string token)
        {
            if(token==null || !Regex.IsMatch(token,@"\AFMFV1\.[A-Za-z0-9_-]{43}\z")) return null;
            using(var hash=SHA256.Create()) return hash.ComputeHash(Encoding.UTF8.GetBytes(token));
        }
    }
}

