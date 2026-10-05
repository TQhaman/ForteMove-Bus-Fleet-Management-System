using System;

namespace ForteMove.Business.Exceptions
{
    public sealed class PassengerPersistenceException : Exception
    {
        public PassengerPersistenceException(string message, string field, Exception innerException)
            : base(message, innerException)
        {
            Field = field ?? string.Empty;
        }

        public string Field { get; private set; }
    }
}
