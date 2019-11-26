// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Generic
{
    public struct ReverseComparable<T> : IComparable<ReverseComparable<T>> where T : IComparable<T>
    {
        public readonly T Value;

        public ReverseComparable(T value)
        {
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(ReverseComparable<T> other)
        {
            return -Value.CompareTo(other.Value);
        }
    }
}