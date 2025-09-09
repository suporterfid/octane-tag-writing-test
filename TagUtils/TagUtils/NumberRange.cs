using System;
using System.Runtime.CompilerServices;

namespace Impinj.TagUtils
{
    public struct NumberRange
    {
        public long Max { get; set; }

        public long Min { get; set; }

        public NumberRange(long min, long max)
        {
            Min = min <= max ? min : throw new ArgumentOutOfRangeException(nameof(min));
            Max = max;
        }

        public NumberRange(long both) => Min = Max = both;

        public long Count => Max - Min + 1L;

        public static NumberRange FromString(string strNumberRange)
        {
            if (string.IsNullOrWhiteSpace(strNumberRange))
                return new NumberRange();
            string[] strArray = strNumberRange.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (strArray.Length < 1 || strArray.Length > 2)
            {
                DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(71, 1);
                interpolatedStringHandler.AppendFormatted(strArray.Length);
                interpolatedStringHandler.AppendLiteral(" Range values specified which is more than the 2 that were anticipated!");
                throw new InvalidCastException(interpolatedStringHandler.ToStringAndClear());
            }
            long first = strArray[0].ToInt64();
            return strArray.Length == 1 ? new NumberRange(first) : new NumberRange(first, strArray[1].ToInt64());
        }

        public string ToHexString()
        {
            return Min != Max ? $"0x{Min:X2}-0x{Max:X2}" : $"0x{Min:X2}";
        }

        public override string ToString()
        {
            return Min != Max ? $"{Min}-{Max}" : $"{Min}";
        }
    }
}
