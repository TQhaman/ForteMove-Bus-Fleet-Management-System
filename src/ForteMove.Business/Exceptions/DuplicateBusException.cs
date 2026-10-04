using System;
using ForteMove.Models.Fleet;

namespace ForteMove.Business.Exceptions
{
    public sealed class DuplicateBusException : Exception
    {
        public DuplicateBusException(DuplicateBusField field)
            : this(field, "A bus with the same unique identifier already exists.", null)
        {
        }

        public DuplicateBusException(DuplicateBusField field, string message)
            : this(field, message, null)
        {
        }

        public DuplicateBusException(DuplicateBusField field, string message, Exception innerException)
            : base(message, innerException)
        {
            Field = field;
        }

        public DuplicateBusField Field { get; private set; }
    }
}
