// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Utils;

namespace Spreads.DataTypes
{
    public readonly partial struct SmallDecimal
    {
#if BUILTIN_SPAN
        /// <summary>
        /// Same as <see cref="decimal.Parse(System.ReadOnlySpan{char},System.Globalization.NumberStyles,System.IFormatProvider?)"/>
        /// with <see cref="NumberStyles.Number"/> and <see cref="CultureInfo.InvariantCulture"/>. For different parameters
        /// first parse to a <see cref="decimal"/> value and then convert it to <see cref="SmallDecimal"/>.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static SmallDecimal Parse(ReadOnlySpan<char> s)
        {
            return new(decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture));
        }
#endif
    }
}
