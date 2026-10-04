using System;

namespace ForteMove.Business.Time
{
    public sealed class SystemClock : IClock
    {
        private static readonly TimeZoneInfo SouthAfricaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }

        public DateTime Today
        {
            get { return OperationalNow.Date; }
        }

        public DateTime OperationalNow
        {
            get
            {
                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    SouthAfricaTimeZone);
            }
        }

        public DateTime ToOperationalTime(DateTime utc)
        {
            DateTime normalized = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(normalized, SouthAfricaTimeZone);
        }
    }
}
