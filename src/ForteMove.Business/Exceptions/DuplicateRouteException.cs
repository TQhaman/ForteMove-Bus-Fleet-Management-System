using System;
using ForteMove.Models.Routing;

namespace ForteMove.Business.Exceptions
{
    public sealed class DuplicateRouteException : Exception
    {
        public DuplicateRouteException(DuplicateRouteField field)
            : this(field, "A route with the same unique value already exists.", null)
        {
        }

        public DuplicateRouteException(DuplicateRouteField field, string message)
            : this(field, message, null)
        {
        }

        public DuplicateRouteException(
            DuplicateRouteField field,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            Field = field;
        }

        public DuplicateRouteField Field { get; private set; }
    }
}
