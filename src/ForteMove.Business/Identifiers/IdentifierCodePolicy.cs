using System;
using System.Globalization;

namespace ForteMove.Business.Identifiers
{
    public static class IdentifierCodePolicy
    {
        public static string FormatFleetNumber(int sequence)
        {
            return Format(sequence, "FM-", "D3");
        }

        public static string FormatRouteCode(int sequence)
        {
            return Format(sequence, "FM-R", "D2");
        }

        public static string FormatStopCode(int sequence)
        {
            return Format(sequence, "ST-", "D3");
        }

        private static string Format(int sequence, string prefix, string format)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "sequence",
                    "The identifier sequence must be greater than zero.");
            }

            return prefix + sequence.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
