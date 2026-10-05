namespace ForteMove.Business.Contracts
{
    public interface IFuelTokenProtector
    {
        byte[] Protect(string token);
        string Unprotect(byte[] protectedToken);
    }
}

