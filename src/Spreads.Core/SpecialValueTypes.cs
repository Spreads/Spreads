/*
    Copyright(c) 2014-2015 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {

    // TODO (@VB) I have somewhere almost complete implementation of Price structs, copy it here

    public struct Price {
        private ulong value;
        private const int precisionOffset = 60;
        private const ulong precisionMask = (2 ^ (64 - precisionOffset) - 1UL) << precisionOffset;
    }

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
