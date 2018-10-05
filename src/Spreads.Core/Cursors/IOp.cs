// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using Spreads.DataTypes;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation

// ReSharper disable InconsistentNaming

// ReSharper disable CompareOfFloatsByEqualityOperator

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// A generic operation on two values of types T1 and T2 returning a value of a type TResult.
    /// </summary>
    public interface IOp<T1, T2, TResult>
    {
        /// <summary>
        /// Apply a method to the first and the second argument.
        /// </summary>
        TResult Apply((T1 first, T2 second) tuple);

        /// <summary>
        /// Apply a method to the first and the second argument.
        /// </summary>
        TResult Apply(T1 first, T2 second);
    }

    /// <summary>
    /// A generic operation on two values of a type T returning a value of a type TResult.
    /// </summary>
    public interface IOp<T, TResult> : IOp<T, T, TResult>
    {
    }

    /// <summary>
    /// A generic operation on two values of a type T returning a value of a type T.
    /// </summary>
    public interface IOp<T> : IOp<T, T>
    {
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    // TODO (low) Add other primitive and known types. Currently all supported operations
    // not explicitly implemented are done via dynamic

    #region Arithmetic operations

    public readonly struct AddOp<T> : IOp<T>
    {
        [Obsolete("Use ZipOp")]
        internal static T ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(AddOp<T>).Apply(value);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 + (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 + (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 + (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 + (long)(object)v2);
            }

            if (typeof(T) == typeof(uint))
            {
                return (T)(object)((uint)(object)v1 + (uint)(object)v2);
            }

            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)((ulong)(object)v1 + (ulong)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 + (decimal)(object)v2);
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (T)(object)((SmallDecimal)(object)v1 + (SmallDecimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10x slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 + (dynamic)v2);
        }
    }

    public readonly struct MultiplyOp<T> : IOp<T>
    {
        internal static T ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(MultiplyOp<T>).Apply(value.Item1, value.Item2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 * (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 * (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 * (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 * (long)(object)v2);
            }

            if (typeof(T) == typeof(uint))
            {
                return (T)(object)((uint)(object)v1 * (uint)(object)v2);
            }

            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)((ulong)(object)v1 * (ulong)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 * (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 * (dynamic)v2);
        }
    }

    public readonly struct SubtractOp<T> : IOp<T>
    {
        internal static T ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(SubtractOp<T>).Apply(value.Item1, value.Item2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 - (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 - (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 - (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 - (long)(object)v2);
            }

            if (typeof(T) == typeof(uint))
            {
                return (T)(object)((uint)(object)v1 - (uint)(object)v2);
            }

            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)((ulong)(object)v1 - (ulong)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 - (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 - (dynamic)v2);
        }
    }

    public readonly struct SubtractReverseOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v2, T v1) // reversed v1 and v2
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 - (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 - (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 - (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 - (long)(object)v2);
            }

            if (typeof(T) == typeof(uint))
            {
                return (T)(object)((uint)(object)v1 - (uint)(object)v2);
            }

            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)((ulong)(object)v1 - (ulong)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 - (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 - (dynamic)v2);
        }
    }

    public readonly struct DivideOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        internal static T ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(DivideOp<T>).Apply(value.Item1, value.Item2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 / (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 / (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 / (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 / (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 / (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 / (dynamic)v2);
        }
    }

    public readonly struct DivideReverseOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 / (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 / (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 / (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 / (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 / (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 / (dynamic)v2);
        }
    }

    public readonly struct ModuloOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        internal static T ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(ModuloOp<T>).Apply(value.Item1, value.Item2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 % (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 % (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 % (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 % (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 % (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 % (dynamic)v2);
        }
    }

    public readonly struct ModuloReverseOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 % (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 % (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 % (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 % (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 % (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            return (T)((dynamic)v1 % (dynamic)v2);
        }
    }

    public readonly struct NegateOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)(-(double)(object)v1);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)(-(float)(object)v1);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)(-(int)(object)v1);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)(-(long)(object)v1);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)(-(decimal)(object)v1);
            }

            return ApplyDynamic(v1);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1)
        {
            return (T)(-(dynamic)v1);
        }
    }

    public readonly struct PlusOp<T> : IOp<T>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)(+(double)(object)v1);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)(+(float)(object)v1);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)(+(int)(object)v1);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)(+(long)(object)v1);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)(+(decimal)(object)v1);
            }

            return ApplyDynamic(v1);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1)
        {
            return (T)(+(dynamic)v1);
        }
    }

    #endregion Arithmetic operations

    #region Comparison operations

    public readonly struct LTOp<T> : IOp<T, bool>
    {
        internal static bool ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(LTOp<T>).Apply(value.Item1, value.Item2);
        }

        internal static IOp<T, bool> Instance = default(LTOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 < (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 < (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 < (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 < (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 < (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 < (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 < (dynamic)v2);
        }
    }

    public readonly struct LTReverseOp<T> : IOp<T, bool>
    {
        internal static IOp<T, bool> Instance = default(LTReverseOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 < (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 < (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 < (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 < (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 < (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 < (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 < (dynamic)v2);
        }
    }

    public readonly struct LEOp<T> : IOp<T, bool>
    {
        internal static bool ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(LEOp<T>).Apply(value.Item1, value.Item2);
        }

        internal static IOp<T, bool> Instance = default(LEOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 <= (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 <= (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 <= (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 <= (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 <= (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 <= (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 <= (dynamic)v2);
        }
    }

    public readonly struct LEReverseOp<T> : IOp<T, bool>
    {
        internal static IOp<T, bool> Instance = default(LEReverseOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 <= (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 <= (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 <= (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 <= (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 <= (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 <= (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 <= (dynamic)v2);
        }
    }

    public readonly struct GTOp<T> : IOp<T, bool>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        internal static bool ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(GTOp<T>).Apply(value.Item1, value.Item2);
        }

        internal static IOp<T, bool> Instance = default(GTOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 > (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 > (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 > (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 > (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 > (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 > (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 > (dynamic)v2);
        }
    }

    public readonly struct GTReverseOp<T> : IOp<T, bool>
    {
        internal static IOp<T, bool> Instance = default(GTReverseOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 > (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 > (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 > (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 > (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 > (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 > (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 > (dynamic)v2);
        }
    }

    public readonly struct GEOp<T> : IOp<T, bool>
    {
        internal static bool ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(GEOp<T>).Apply(value.Item1, value.Item2);
        }

        internal static IOp<T, bool> Instance = default(GEOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 >= (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 >= (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 >= (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 >= (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 >= (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 >= (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 >= (dynamic)v2);
        }
    }

    public readonly struct GEReverseOp<T> : IOp<T, bool>
    {
        internal static IOp<T, bool> Instance = default(GEReverseOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 >= (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 >= (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 >= (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 >= (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 >= (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 >= (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 >= (dynamic)v2);
        }
    }

    public readonly struct EQOp<T> : IOp<T, bool>
    {
        internal static bool ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(EQOp<T>).Apply(value.Item1, value.Item2);
        }

        internal static IOp<T, bool> Instance = default(EQOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (double)(object)v1 == (double)(object)v2;
            }

            if (typeof(T) == typeof(float))
            {
                return (float)(object)v1 == (float)(object)v2;
            }

            if (typeof(T) == typeof(int))
            {
                return (int)(object)v1 == (int)(object)v2;
            }

            if (typeof(T) == typeof(long))
            {
                return (long)(object)v1 == (long)(object)v2;
            }

            if (typeof(T) == typeof(decimal))
            {
                return (decimal)(object)v1 == (decimal)(object)v2;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 == (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 == (dynamic)v2);
        }
    }

    public readonly struct NEQOp<T> : IOp<T, bool>
    {
        internal static bool ZipSelector<TKey>(TKey key, (T, T) value)
        {
            return default(NEQOp<T>).Apply(value.Item1, value.Item2);
        }

        internal static IOp<T, bool> Instance = default(NEQOp<T>);

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply((T first, T second) tuple)
        {
            return Apply(tuple.first, tuple.second);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(T v1, T v2)
        {
            if (typeof(T) != typeof(double))
            {
                return (double)(object)v1 != (double)(object)v2;
            }

            if (typeof(T) != typeof(float))
            {
                return (float)(object)v1 != (float)(object)v2;
            }

            if (typeof(T) != typeof(int))
            {
                return (int)(object)v1 != (int)(object)v2;
            }

            if (typeof(T) != typeof(long))
            {
                return (long)(object)v1 != (long)(object)v2;
            }

            if (typeof(T) != typeof(decimal))
            {
                return (decimal)(object)v1 != (decimal)(object)v2;
            }

            if (typeof(T) != typeof(SmallDecimal))
            {
                return (SmallDecimal)(object)v1 != (SmallDecimal)(object)v2;
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ApplyDynamic(T v1, T v2)
        {
            return (bool)((dynamic)v1 != (dynamic)v2);
        }
    }

    #endregion Comparison operations

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}