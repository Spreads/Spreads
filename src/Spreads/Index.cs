﻿using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public struct Index
    {
        private long Idx;

        public Index(long idx)
        {
            Idx = idx;
        }

#if HAS_RANGE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Index(System.Index index)
        {
            return new Index(index.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Index(long index)
        {
            return new System.Index(checked((int)index));
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator long(Index index)
        {
            return index.Idx;
        }
    }
}
