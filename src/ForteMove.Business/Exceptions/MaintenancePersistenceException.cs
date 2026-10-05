using System;
namespace ForteMove.Business.Exceptions
{
    public sealed class MaintenancePersistenceException : Exception
    {
        public MaintenancePersistenceException(string message) : base(message) { }
        public MaintenancePersistenceException(string message,Exception inner) : base(message,inner) { }
    }
}
