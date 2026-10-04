using System;

namespace ForteMove.Business.Exceptions
{
    public sealed class DriverPersistenceException : Exception
    {
        public DriverPersistenceException(string field, string message, Exception innerException)
            : base(message, innerException)
        {
            Field = field ?? string.Empty;
        }

        public string Field { get; private set; }
    }
}
