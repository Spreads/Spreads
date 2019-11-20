// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

#if HAS_INTRINSICS

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#endif

namespace Spreads.Algorithms
{
    // TODOs operations on generics
    //            Scalars   Aggregates     Vec
    // Add/Sum      x           -           -
    // Sub                      n.a.
    // Mul/Prod
    // TODO move from ops
    // At first need to make batching right, so work it out with Add/Sum

    /// <summary>
    /// Hardware-accelerated simple math operations.
    /// </summary>
    /// <remarks>
    /// WARNING: Methods in this static class do not perform bound checks and are intended to be used
    /// as building blocks in other parts that calculate bounds correctly and
    /// do performs required checks on external input.
    /// </remarks>
    public static class VectorMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T AddScalars<T>(T v1, T v2)
        {
            if (typeof(T) == typeof(byte))
            {
                return (T)(object)((byte)(object)v1 + (byte)(object)v2);
            }
            if (typeof(T) == typeof(sbyte))
            {
                return (T)(object)((sbyte)(object)v1 + (sbyte)(object)v2);
            }
            if (typeof(T) == typeof(ushort))
            {
                return (T)(object)((ushort)(object)v1 + (ushort)(object)v2);
            }
            if (typeof(T) == typeof(short))
            {
                return (T)(object)((short)(object)v1 + (short)(object)v2);
            }
            if (typeof(T) == typeof(uint))
            {
                return (T)(object)((uint)(object)v1 + (uint)(object)v2);
            }
            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 + (int)(object)v2);
            }
            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)((ulong)(object)v1 + (ulong)(object)v2);
            }
            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 + (long)(object)v2);
            }
            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 + (float)(object)v2);
            }
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 + (double)(object)v2);
            }

            // TODO known types that support addition

            return AddScalarsDynamic(v1, v2);
        }

        internal static T AddScalarsDynamic<T>(T v1, T v2)
        {
            // NB this is 5-10x slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 + (dynamic)v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Sum<T>(ref T vecStart, int length)
        {
            if (Vector.IsHardwareAccelerated)
            {
                // TODO
            }

            return SumSimple(ref vecStart, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T SumSimple<T>(ref T vecStart, int length)
        {
            var acc = vecStart;
            for (int i = 1; i < length; i++)
            {
                acc = AddScalars(acc, Unsafe.Add(ref vecStart, i));
            }

            return acc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T SumVectorized<T>(ref T vecStart, int length)
        {
            throw new NotImplementedException();
        }
    }
}
