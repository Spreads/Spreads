using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Slices {
    public static partial class SpanExtensions {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this Span<T> span, int index, int length, T value, IComparer<T> comparer) {

            if (span.Object != null) {
                var offset = index > 0 ? ((int)span.Offset.ToUInt32() - SpanHelpers<T>.OffsetToArrayData) / PtrUtils.SizeOf<T>() : 0;
                // by construction if Object is not null it is T[], fail if it is not when casting
                return Array.BinarySearch<T>((T[])span.Object, offset + index, length, value, comparer);
            }

            if ((uint)(index) + (uint)length > (uint)span.Length) {
                throw new ArgumentException("Index + Length fall outside the span boundary.");
            }

            if (comparer == null) {
                comparer = Comparer<T>.Default;
            }

            int lo = index;
            int hi = index + length - 1;
            while (lo <= hi) {
                int i = lo + ((hi - lo) >> 1);
                int order = comparer.Compare(span.GetUnsafe(i), value);

                if (order == 0) return i;
                if (order < 0) {
                    lo = i + 1;
                } else {
                    hi = i - 1;
                }
            }
            return ~lo;
        }

    }
}
