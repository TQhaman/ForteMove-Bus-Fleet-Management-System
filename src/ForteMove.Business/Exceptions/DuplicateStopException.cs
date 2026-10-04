using System;
using ForteMove.Models.Routing;

namespace ForteMove.Business.Exceptions
{
    public sealed class DuplicateStopException : Exception
    {
        public DuplicateStopException(DuplicateStopField field)
            : this(field, "A stop with the same unique value already exists.", null)
        {
        }

        public DuplicateStopException(DuplicateStopField field, string message)
            : this(field, message, null)
        {
        }

        public DuplicateStopException(
            DuplicateStopField field,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            Field = field;
        }

        public DuplicateStopField Field { get; private set; }
    }
}
