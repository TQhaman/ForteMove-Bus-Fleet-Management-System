using System.Security.Cryptography;
using System.Text;
using System.Web.Security;
using ForteMove.Business.Contracts;
namespace ForteMove.Web.Infrastructure
{
    public sealed class MachineKeyFuelTokenProtector : IFuelTokenProtector
    {
        public byte[] Protect(string token) { return MachineKey.Protect(Encoding.UTF8.GetBytes(token),"ForteMove.FuelVoucher.Redemption.v1"); }
        public string Unprotect(byte[] value)
        {
            byte[] decoded=MachineKey.Unprotect(value,"ForteMove.FuelVoucher.Redemption.v1");
            if(decoded==null) throw new CryptographicException("Voucher protection keys are unavailable.");
            return Encoding.UTF8.GetString(decoded);
        }
    }
}
