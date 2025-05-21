using System;
using System.Globalization;
using System.Numerics;

namespace EpcListGenerator
{
    public class MonzaTidParser : IDisposable
    {
        private readonly byte[] _tid;
        private bool _disposed;

        public MonzaTidParser(string tidHex)
        {
            if (tidHex == null) throw new ArgumentNullException(nameof(tidHex));
            if (tidHex.Length != 24)
                throw new ArgumentException("TID deve conter exatamente 24 caracteres hexadecimais (96 bits).", nameof(tidHex));

            _tid = new byte[12];
            for (int i = 0; i < 12; i++)
            {
                _tid[i] = byte.Parse(tidHex.Substring(i * 2, 2), NumberStyles.HexNumber);
            }
        }

        public string Get38BitSerialHex()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MonzaTidParser));

            if (!IsImpinjTid())
            {
                return GetFallbackSerialHex();
            }

            int offsetLast13Bits = IsMonza6() ? 6 : 6; // mesmo offset, mas pode variar se usar bit shift

            int bits55_5F = (_tid[10] << 8) | _tid[11]; // 16 bits
            int bits40_4F = (_tid[8] << 8) | _tid[9];   // 16 bits
            int bitsX3F = (_tid[offsetLast13Bits] << 8) | _tid[offsetLast13Bits + 1];
            int last13Bits = bitsX3F & 0x1FFF;

            BigInteger serial = ((BigInteger)bits55_5F << 22) | ((BigInteger)bits40_4F << 6) | last13Bits;
            return serial.ToString("X10");
        }

        private bool IsImpinjTid()
        {
            // Verifica se começa com E2801xxx
            return _tid[0] == 0xE2 && _tid[1] == 0x80 && (_tid[2] & 0xF0) == 0x10;
        }

        private bool IsMonza6()
        {
            // TID modelo Monza 6: bits 14h–1Fh = 0x1700, que são _tid[2] = 0x11 e _tid[3] = 0x70
            return _tid[2] == 0x11 && _tid[3] == 0x70;
        }

        private string GetFallbackSerialHex()
        {
            return BitConverter.ToString(_tid, 7, 5).Replace("-", "");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
