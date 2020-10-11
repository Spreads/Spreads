// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Native;
using System.Runtime.CompilerServices;
#if HAS_INTRINSICS
using Spreads.Utils;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

// ReSharper disable AccessToStaticMemberViaDerivedType
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable RedundantCast

namespace Spreads.Algorithms {

    public static partial class VectorSearch
    {

#if HAS_INTRINSICS

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx2LoHi(ref sbyte searchSpace, int lo, int hi, sbyte value)
        {
            unchecked
            {
                int mask;

                sbyte vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector256<sbyte>.Count)
                    goto LINEAR;

                Vector256<sbyte> vecI;
                Vector256<sbyte> gt;
                var valVec = Vector256.Create(value);
                
                // x3 is needed to safely fall into linear vectorized search:
                // after this loop all possible lo/hi are valid for linear vectorized search
                while (hi - lo >= Vector256<sbyte>.Count * 3) 
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector256<sbyte>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector256<sbyte>>(ref Unsafe.As<sbyte, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((uint) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<sbyte>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }

                        // val is not greater than all in vec
                        // not i-1, i could equal;
                        hi = i;
                    }
                    else
                    {
                        // val is larger than all in vec
                        lo = i + Vector256<sbyte>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector256<sbyte>>(ref Unsafe.As<sbyte, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    var clz = BitUtils.LeadingZeroCount(mask);
                    var index = (32 - clz) / Unsafe.SizeOf<sbyte>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector256<sbyte>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx2LoHi(ref short searchSpace, int lo, int hi, short value)
        {
            unchecked
            {
                int mask;

                short vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector256<short>.Count)
                    goto LINEAR;

                Vector256<short> vecI;
                Vector256<short> gt;
                var valVec = Vector256.Create(value);
                
                // x3 is needed to safely fall into linear vectorized search:
                // after this loop all possible lo/hi are valid for linear vectorized search
                while (hi - lo >= Vector256<short>.Count * 3) 
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector256<short>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector256<short>>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((uint) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<short>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }

                        // val is not greater than all in vec
                        // not i-1, i could equal;
                        hi = i;
                    }
                    else
                    {
                        // val is larger than all in vec
                        lo = i + Vector256<short>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector256<short>>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    var clz = BitUtils.LeadingZeroCount(mask);
                    var index = (32 - clz) / Unsafe.SizeOf<short>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector256<short>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx2LoHi(ref int searchSpace, int lo, int hi, int value)
        {
            unchecked
            {
                int mask;

                int vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector256<int>.Count)
                    goto LINEAR;

                Vector256<int> vecI;
                Vector256<int> gt;
                var valVec = Vector256.Create(value);
                
                // x3 is needed to safely fall into linear vectorized search:
                // after this loop all possible lo/hi are valid for linear vectorized search
                while (hi - lo >= Vector256<int>.Count * 3) 
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector256<int>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector256<int>>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((uint) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<int>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }

                        // val is not greater than all in vec
                        // not i-1, i could equal;
                        hi = i;
                    }
                    else
                    {
                        // val is larger than all in vec
                        lo = i + Vector256<int>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector256<int>>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    var clz = BitUtils.LeadingZeroCount(mask);
                    var index = (32 - clz) / Unsafe.SizeOf<int>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector256<int>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx2LoHi(ref long searchSpace, int lo, int hi, long value)
        {
            unchecked
            {
                int mask;

                long vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector256<long>.Count)
                    goto LINEAR;

                Vector256<long> vecI;
                Vector256<long> gt;
                var valVec = Vector256.Create(value);
                
                // x3 is needed to safely fall into linear vectorized search:
                // after this loop all possible lo/hi are valid for linear vectorized search
                while (hi - lo >= Vector256<long>.Count * 3) 
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector256<long>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((uint) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<long>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }

                        // val is not greater than all in vec
                        // not i-1, i could equal;
                        hi = i;
                    }
                    else
                    {
                        // val is larger than all in vec
                        lo = i + Vector256<long>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    var clz = BitUtils.LeadingZeroCount(mask);
                    var index = (32 - clz) / Unsafe.SizeOf<long>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector256<long>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	
    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchSse42LoHi(ref sbyte searchSpace, int lo, int hi, sbyte value)
        {
            // Avx2 modification with mask as short and it's special handling 
            unchecked
            {
                short mask;

                sbyte vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector128<sbyte>.Count)
                    goto LINEAR;

                Vector128<sbyte> vecI;
                Vector128<sbyte> gt;
                var valVec = Vector128.Create(value);
                while (hi - lo >= Vector128<sbyte>.Count * 3)
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector128<sbyte>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector128<sbyte>>(ref Unsafe.As<sbyte, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((ushort) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<sbyte>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }
                        hi = i;
                    }
                    else
                    {
                        lo = i + Vector128<sbyte>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector128<sbyte>>(ref Unsafe.As<sbyte, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    var clz = BitUtils.LeadingZeroCount((ushort)mask);
                    var index = (32 - clz) / Unsafe.SizeOf<sbyte>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector128<sbyte>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchSse42LoHi(ref short searchSpace, int lo, int hi, short value)
        {
            // Avx2 modification with mask as short and it's special handling 
            unchecked
            {
                short mask;

                short vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector128<short>.Count)
                    goto LINEAR;

                Vector128<short> vecI;
                Vector128<short> gt;
                var valVec = Vector128.Create(value);
                while (hi - lo >= Vector128<short>.Count * 3)
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector128<short>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((ushort) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<short>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }
                        hi = i;
                    }
                    else
                    {
                        lo = i + Vector128<short>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector128<short>>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    var clz = BitUtils.LeadingZeroCount((ushort)mask);
                    var index = (32 - clz) / Unsafe.SizeOf<short>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector128<short>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchSse42LoHi(ref int searchSpace, int lo, int hi, int value)
        {
            // Avx2 modification with mask as short and it's special handling 
            unchecked
            {
                short mask;

                int vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector128<int>.Count)
                    goto LINEAR;

                Vector128<int> vecI;
                Vector128<int> gt;
                var valVec = Vector128.Create(value);
                while (hi - lo >= Vector128<int>.Count * 3)
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector128<int>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector128<int>>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((ushort) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<int>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }
                        hi = i;
                    }
                    else
                    {
                        lo = i + Vector128<int>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector128<int>>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    var clz = BitUtils.LeadingZeroCount((ushort)mask);
                    var index = (32 - clz) / Unsafe.SizeOf<int>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector128<int>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchSse42LoHi(ref long searchSpace, int lo, int hi, long value)
        {
            // Avx2 modification with mask as short and it's special handling 
            unchecked
            {
                short mask;

                long vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector128<long>.Count)
                    goto LINEAR;

                Vector128<long> vecI;
                Vector128<long> gt;
                var valVec = Vector128.Create(value);
                while (hi - lo >= Vector128<long>.Count * 3)
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector128<long>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector128<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((ushort) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<long>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }
                        hi = i;
                    }
                    else
                    {
                        lo = i + Vector128<long>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector128<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    var clz = BitUtils.LeadingZeroCount((ushort)mask);
                    var index = (32 - clz) / Unsafe.SizeOf<long>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector128<long>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchSse42LoHi(ref double searchSpace, int lo, int hi, double value)
        {
            // Avx2 modification with mask as short and it's special handling 
            unchecked
            {
                short mask;

                double vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector128<double>.Count)
                    goto LINEAR;

                Vector128<double> vecI;
                Vector128<double> gt;
                var valVec = Vector128.Create(value);
                while (hi - lo >= Vector128<double>.Count * 3)
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector128<double>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector128<double>>(ref Unsafe.As<double, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((ushort) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<double>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }
                        hi = i;
                    }
                    else
                    {
                        lo = i + Vector128<double>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector128<double>>(ref Unsafe.As<double, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    var clz = BitUtils.LeadingZeroCount((ushort)mask);
                    var index = (32 - clz) / Unsafe.SizeOf<double>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector128<double>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchSse42LoHi(ref float searchSpace, int lo, int hi, float value)
        {
            // Avx2 modification with mask as short and it's special handling 
            unchecked
            {
                short mask;

                float vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector128<float>.Count)
                    goto LINEAR;

                Vector128<float> vecI;
                Vector128<float> gt;
                var valVec = Vector128.Create(value);
                while (hi - lo >= Vector128<float>.Count * 3)
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector128<float>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.Add(ref searchSpace, i)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) BitUtils.LeadingZeroCount((ushort) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<float>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }
                        hi = i;
                    }
                    else
                    {
                        lo = i + Vector128<float>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector128<float>>(ref Unsafe.As<float, byte>(ref Unsafe.Add(ref searchSpace, lo)));
                    gt = Sse42.CompareGreaterThan(valVec, vecI);
                    mask = (short)(((1u << 16) - 1) & (uint)Sse42.MoveMask(gt.AsByte()));

                    var clz = BitUtils.LeadingZeroCount((ushort)mask);
                    var index = (32 - clz) / Unsafe.SizeOf<float>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector128<float>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

	
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref sbyte searchSpace, int lo, int hi, sbyte value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<sbyte>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref byte searchSpace, int lo, int hi, byte value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<byte>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref short searchSpace, int lo, int hi, short value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<short>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref ushort searchSpace, int lo, int hi, ushort value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<ushort>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref char searchSpace, int lo, int hi, char value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<char>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref int searchSpace, int lo, int hi, int value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<int>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref uint searchSpace, int lo, int hi, uint value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<uint>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref long searchSpace, int lo, int hi, long value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<long>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref ulong searchSpace, int lo, int hi, ulong value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<ulong>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref double searchSpace, int lo, int hi, double value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<double>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	    [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int InterpolationSearchSpecializedLoHi(ref float searchSpace, int lo, int hi, float value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;
                
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<float>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    double vRange = vhi - vLo;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    var nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to searchSpace
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            
                            if (i > hi)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value <= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;
                            
                            if (i < lo)
                                break;

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

	}
}
