using System;

namespace ForteMove.Business.Exceptions
{
    public sealed class UnavailableRouteStopException : Exception
    {
        public UnavailableRouteStopException(long stopId)
            : this(stopId, "A selected stop is no longer available.", null)
        {
        }

        public UnavailableRouteStopException(
            long stopId,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            StopId = stopId;
        }

        public long StopId { get; private set; }
    }
}
