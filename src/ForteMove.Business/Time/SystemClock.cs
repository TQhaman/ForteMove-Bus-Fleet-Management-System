using System;

namespace ForteMove.Business.Time
{
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        public DateTime Today
        {
            get { return DateTime.Today; }
        }
    }
}
