using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;


#nullable enable
namespace Impinj.Utils
{
    public class Hexadecimal
    {
        private
#nullable disable
        readonly StringBuilder _value;

        public int Length => _value.Length;

        public Hexadecimal(string value)
        {
            string str1 = !string.IsNullOrWhiteSpace(value) ? value.Trim().ToLower() : throw new ArgumentNullException(nameof(value));
            string source = (str1.StartsWith("0x") ? str1.Substring(2) : str1).Replace(" ", string.Empty).Replace("-", string.Empty);
            string str2 = "Error Converting string '" + value + "' to hexadecimal!";
            if (source.Any(c =>
            {
                if (c >= '0' && c <= '9')
                    return false;
                return c < 'a' || c > 'f';
            }))
                throw new ArgumentException(str2 + " Only hex digits (including those beginning with '0x') are supported!");
            _value = source.All(c =>
            {
                if (c >= '0' && c <= '9')
                    return true;
                return c >= 'a' && c <= 'f';
            }) ? new StringBuilder(source) : throw new ArgumentNullException(str2 + " No Hexadecimal characters found!");
        }

        public static implicit operator string(Hexadecimal d) => d.ToString();

        public static implicit operator Hexadecimal(string s) => new Hexadecimal(s);

        public static implicit operator BitArray(Hexadecimal d) => d.ToBitArray();

        public static implicit operator long(Hexadecimal d) => long.Parse(d.ToRawHexString(), NumberStyles.HexNumber);

        public static implicit operator Hexadecimal(long b) => new Hexadecimal(b.ToString("X"));

        public static implicit operator int(Hexadecimal d) => int.Parse(d.ToRawHexString(), NumberStyles.HexNumber);

        public static implicit operator Hexadecimal(int b) => new Hexadecimal(b.ToString("X"));

        public override string ToString()
        {
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 1);
            interpolatedStringHandler.AppendLiteral("0x");
            interpolatedStringHandler.AppendFormatted(ToRawHexString(), "X2");
            return interpolatedStringHandler.ToStringAndClear();
        }

        public string ToRawHexString() => _value.ToString();

        public BitArray ToBitArray()
        {
            bool[] bits = new bool[_value.Length * 4];
            for (int i = 0; i < _value.Length; i++)
            {
                char c = _value[i];
                int value = c <= '9' ? c - '0' : c - 'a' + 10;
                int index = i * 4;
                bits[index] = (value & 8) != 0;
                bits[index + 1] = (value & 4) != 0;
                bits[index + 2] = (value & 2) != 0;
                bits[index + 3] = (value & 1) != 0;
            }
            return new BitArray(bits);
        }
    }
}
