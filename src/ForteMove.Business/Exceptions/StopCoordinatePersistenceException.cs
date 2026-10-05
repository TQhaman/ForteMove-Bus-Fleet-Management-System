using System;
namespace ForteMove.Business.Exceptions
{
    public sealed class StopCoordinatePersistenceException : Exception
    {
        public StopCoordinatePersistenceException(string message) : base(message) { }
    }
}
