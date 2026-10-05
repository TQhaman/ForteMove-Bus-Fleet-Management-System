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

        public static string FormatScheduleCode(int sequence)
        {
            return Format(sequence, "FM-S", "D2");
        }

        public static string FormatDriverEmployeeNumber(int sequence)
        {
            return Format(sequence, "DRV-", "D3");
        }

        public static string FormatTripCode(long sequence)
        {
            if (sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "sequence",
                    "The identifier sequence must be greater than zero.");
            }

            return "TR-" + sequence.ToString("D6", CultureInfo.InvariantCulture);
        }

        public static string FormatDefectCode(long sequence)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException("sequence", "The identifier sequence must be greater than zero.");
            return "DF-" + sequence.ToString("D6", CultureInfo.InvariantCulture);
        }

        public static string FormatTicketCode(long sequence)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException("sequence", "The identifier sequence must be greater than zero.");
            return "TKT-" + sequence.ToString("D6", CultureInfo.InvariantCulture);
        }

        public static string FormatWalletTransactionCode(long sequence)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException("sequence", "The identifier sequence must be greater than zero.");
            return "WTX-" + sequence.ToString("D6", CultureInfo.InvariantCulture);
        }

        public static string FormatFuelRequestCode(long sequence) { return FuelCode(sequence,"FR-","D6"); }
        public static string FormatFuelVoucherCode(long sequence) { return FuelCode(sequence,"FV-","D6"); }
        public static string FormatFuelTransactionCode(long sequence) { return FuelCode(sequence,"FTX-","D6"); }
        public static string FormatFuelStationCode(long sequence) { return FuelCode(sequence,"FS-","D3"); }
        private static string FuelCode(long sequence,string prefix,string format)
        {
            if(sequence<=0) throw new ArgumentOutOfRangeException("sequence");
            return prefix+sequence.ToString(format,CultureInfo.InvariantCulture);
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
