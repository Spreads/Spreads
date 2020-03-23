// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if HAS_INTRINSICS

using Spreads.Native;
using System.Runtime.CompilerServices;
using Spreads.Utils;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Spreads.Algorithms {

    public static partial class VectorSearch
    {
	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx(ref sbyte vecStart, int length, sbyte value)
        {
            unchecked
            {
                int i;
                int c;
                int lo = 0;
                int hi = length - 1;
                var valVec = Vector256.Create(value);
                while (hi - lo > Vector256<sbyte>.Count - 1)
                {
                    i = (int) (((uint) hi + (uint) lo) >> 1) - (Vector256<sbyte>.Count >> 1);

                    var vec = Unsafe.ReadUnaligned<Vector256<sbyte>>(ref Unsafe.As<sbyte, byte>(ref Unsafe.Add(ref vecStart, i)));

                    // AVX512 has _mm256_cmpge_epi64_mask that should allow to combine the two operations
                    // and avoid edge-case check in `mask == 0` case below
                    var gt = Avx2.CompareGreaterThan(valVec, vec); // _mm256_cmpgt_epi64
                    var mask = Avx2.MoveMask(gt.AsByte());

                    if (mask == 0) // val is smaller than all in vec
                    {
                        // but could be equal to the first element
                        c = value.CompareTo(vec.GetElement(0));
                        if (c == 0)
                        {
                            lo = i;
                            goto RETURN;
                        }

                        hi = i - 1;
                    }
                    else if (mask == -1) // val is larger than all in vec
                    {
                        lo = i + Vector256<sbyte>.Count;
                    }
                    else
                    {
                        var clz = BitUtil.NumberOfLeadingZeros(mask);
                        var index = (32 - clz) / Unsafe.SizeOf<sbyte>();
                        lo = i + index;
                        c = value.CompareTo(vec.GetElement(index));
                        goto RETURN;
                    }
                }

                while ((c = value.CompareTo(UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)))) > 0
                       & ++lo <= hi) // if using branchless & then need to correct lo below
                {
                }

                lo -= UnsafeEx.Clt(c, 1); // correct back non-short-circuit & evaluation

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(c, 0);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx(ref short vecStart, int length, short value)
        {
            unchecked
            {
                int i;
                int c;
                int lo = 0;
                int hi = length - 1;
                var valVec = Vector256.Create(value);
                while (hi - lo > Vector256<short>.Count - 1)
                {
                    i = (int) (((uint) hi + (uint) lo) >> 1) - (Vector256<short>.Count >> 1);

                    var vec = Unsafe.ReadUnaligned<Vector256<short>>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref vecStart, i)));

                    // AVX512 has _mm256_cmpge_epi64_mask that should allow to combine the two operations
                    // and avoid edge-case check in `mask == 0` case below
                    var gt = Avx2.CompareGreaterThan(valVec, vec); // _mm256_cmpgt_epi64
                    var mask = Avx2.MoveMask(gt.AsByte());

                    if (mask == 0) // val is smaller than all in vec
                    {
                        // but could be equal to the first element
                        c = value.CompareTo(vec.GetElement(0));
                        if (c == 0)
                        {
                            lo = i;
                            goto RETURN;
                        }

                        hi = i - 1;
                    }
                    else if (mask == -1) // val is larger than all in vec
                    {
                        lo = i + Vector256<short>.Count;
                    }
                    else
                    {
                        var clz = BitUtil.NumberOfLeadingZeros(mask);
                        var index = (32 - clz) / Unsafe.SizeOf<short>();
                        lo = i + index;
                        c = value.CompareTo(vec.GetElement(index));
                        goto RETURN;
                    }
                }

                while ((c = value.CompareTo(UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)))) > 0
                       & ++lo <= hi) // if using branchless & then need to correct lo below
                {
                }

                lo -= UnsafeEx.Clt(c, 1); // correct back non-short-circuit & evaluation

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(c, 0);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx(ref int vecStart, int length, int value)
        {
            unchecked
            {
                int i;
                int c;
                int lo = 0;
                int hi = length - 1;
                var valVec = Vector256.Create(value);
                while (hi - lo > Vector256<int>.Count - 1)
                {
                    i = (int) (((uint) hi + (uint) lo) >> 1) - (Vector256<int>.Count >> 1);

                    var vec = Unsafe.ReadUnaligned<Vector256<int>>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref vecStart, i)));

                    // AVX512 has _mm256_cmpge_epi64_mask that should allow to combine the two operations
                    // and avoid edge-case check in `mask == 0` case below
                    var gt = Avx2.CompareGreaterThan(valVec, vec); // _mm256_cmpgt_epi64
                    var mask = Avx2.MoveMask(gt.AsByte());

                    if (mask == 0) // val is smaller than all in vec
                    {
                        // but could be equal to the first element
                        c = value.CompareTo(vec.GetElement(0));
                        if (c == 0)
                        {
                            lo = i;
                            goto RETURN;
                        }

                        hi = i - 1;
                    }
                    else if (mask == -1) // val is larger than all in vec
                    {
                        lo = i + Vector256<int>.Count;
                    }
                    else
                    {
                        var clz = BitUtil.NumberOfLeadingZeros(mask);
                        var index = (32 - clz) / Unsafe.SizeOf<int>();
                        lo = i + index;
                        c = value.CompareTo(vec.GetElement(index));
                        goto RETURN;
                    }
                }

                while ((c = value.CompareTo(UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)))) > 0
                       & ++lo <= hi) // if using branchless & then need to correct lo below
                {
                }

                lo -= UnsafeEx.Clt(c, 1); // correct back non-short-circuit & evaluation

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(c, 0);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx(ref long vecStart, int length, long value)
        {
            unchecked
            {
                int i;
                int c;
                int lo = 0;
                int hi = length - 1;
                var valVec = Vector256.Create(value);
                while (hi - lo > Vector256<long>.Count - 1)
                {
                    i = (int) (((uint) hi + (uint) lo) >> 1) - (Vector256<long>.Count >> 1);

                    var vec = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));

                    // AVX512 has _mm256_cmpge_epi64_mask that should allow to combine the two operations
                    // and avoid edge-case check in `mask == 0` case below
                    var gt = Avx2.CompareGreaterThan(valVec, vec); // _mm256_cmpgt_epi64
                    var mask = Avx2.MoveMask(gt.AsByte());

                    if (mask == 0) // val is not greater than all in vec
                    {
                        // but could be equal to the first element
                        c = value.CompareTo(vec.GetElement(0));
                        if (c == 0)
                        {
                            lo = i;
                            goto RETURN;
                        }

                        hi = i - 1;
                    }
                    else if (mask == -1) // val is larger than all in vec
                    {
                        lo = i + Vector256<long>.Count;
                    }
                    else
                    {
                        var clz = BitUtil.NumberOfLeadingZeros(mask);
                        var index = (32 - clz) / Unsafe.SizeOf<long>();
                        lo = i + index;
                        c = value.CompareTo(vec.GetElement(index));
                        goto RETURN;
                    }
                }

                while ((c = value.CompareTo(UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)))) > 0
                       & ++lo <= hi) // if using branchless & then need to correct lo below
                {
                }

                lo -= UnsafeEx.Clt(c, 1); // correct back non-short-circuit & evaluation

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(c, 0);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	}
}

#endif