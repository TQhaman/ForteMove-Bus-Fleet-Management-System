using System;

namespace ForteMove.Business.Exceptions
{
    public sealed class SchedulingConflictException : Exception
    {
        public SchedulingConflictException(string message) : base(message) { }
    }

    public sealed class SchedulingConcurrencyException : Exception
    {
        public SchedulingConcurrencyException(string message) : base(message) { }
    }

    public sealed class SchedulingReferenceException : Exception
    {
        public SchedulingReferenceException(string message) : base(message) { }
    }
}
