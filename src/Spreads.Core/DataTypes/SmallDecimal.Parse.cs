// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;

namespace Spreads.DataTypes
{
    public readonly partial struct SmallDecimal
    {
#if BUILTIN_SPAN
        /// <summary>
        /// Same as <see cref="decimal.Parse(System.ReadOnlySpan{char},System.Globalization.NumberStyles,System.IFormatProvider?)"/>
        /// with <see cref="NumberStyles.Number"/> | <see cref="NumberStyles.AllowExponent"/> and <see cref="CultureInfo.InvariantCulture"/>. For different parameters
        /// first parse to a <see cref="decimal"/> value and then convert it to <see cref="SmallDecimal"/>.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SmallDecimal Parse(ReadOnlySpan<char> s)
        {
            // We need AllowExponent because some sources (e.g. BBG API) could send what is logically decimal
            // with an exponent notation.
            return new(decimal.Parse(s, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture));
        }
#endif
    }
}
