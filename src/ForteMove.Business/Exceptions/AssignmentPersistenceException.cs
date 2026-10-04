using System;

namespace ForteMove.Business.Exceptions
{
    public sealed class AssignmentPersistenceException : Exception
    {
        public AssignmentPersistenceException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
