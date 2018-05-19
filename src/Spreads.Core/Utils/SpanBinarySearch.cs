// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Utils
{
    internal static class SpanExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this Span<T> span, int index, int length, T value, KeyComparer<T> comparer)
        {
            if ((uint)(index) + (uint)length > (uint)span.Length)
            {
                ThrowHelper.ThrowArgumentException("Index + Length fall outside the span boundary.");
            }

            int lo = index;
            int hi = index + length - 1;
            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                int order = comparer.Compare(span[i], value);

                if (order == 0) return i;
                if (order < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }
            return ~lo;
        }
    }
}