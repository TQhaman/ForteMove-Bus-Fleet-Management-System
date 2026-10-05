using System;
namespace ForteMove.Business.Exceptions
{
    public sealed class FuelPersistenceException : Exception
    {
        public FuelPersistenceException(string message,Exception inner=null) : base(message,inner) { }
    }
}

