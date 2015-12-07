using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {

    /// <summary>
    /// Precise decimal DTO type for prices and similar values.
    /// </summary>
    /// <remarks>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |S|       |              Int60 value (ex. sign)                 |
    /// +-------------------------------+-+-+---------------------------+
    /// |               Int60 value (ex. sign)                          |
    /// +-------------------------------+-+-+---------------------------+
    /// </remarks>
    public struct SmallDecimal
    {
        private const int ExponentOffset = 58;
        private const long ExponentMask = 15L << ExponentOffset;
        private const long MantissaMask = ~~ExponentMask; //(1UL << ExponentOffset) - 1UL;

        private readonly long _value;

        public long Mantissa => _value & MantissaMask;
        public byte Exponent => (byte)((_value & ExponentMask) >> ExponentOffset);


        // constructor with mantissa/exponent, implicit + explicit conversions to/from decimal, to double/long/etc
        // roundUp/Down for decimal/double to the nearest multiple of the minimum step

    }



}
