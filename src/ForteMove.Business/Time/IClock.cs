using System;

namespace ForteMove.Business.Time
{
    public interface IClock
    {
        DateTime UtcNow { get; }

        DateTime Today { get; }

        DateTime OperationalNow { get; }

        DateTime ToOperationalTime(DateTime utc);
    }
}
