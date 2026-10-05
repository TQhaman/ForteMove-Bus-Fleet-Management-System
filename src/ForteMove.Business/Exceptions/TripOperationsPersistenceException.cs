using System;

namespace ForteMove.Business.Exceptions
{
    public sealed class TripOperationsPersistenceException : Exception
    {
        public TripOperationsPersistenceException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
