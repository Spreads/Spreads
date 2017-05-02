// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Spreads.Cursors
{
    /// <summary>
    /// A generic operation on two values of types T1 and T2 returning a value of a type TResult.
    /// </summary>
    public interface IOp<T1, T2, TResult>
    {
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

    public struct AddOp<T> : IOp<T>
    {
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

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 + (decimal)(object)v2);
            }

            if (typeof(T) == typeof(Price))
            {
                return (T)(object)((Price)(object)v1 + (Price)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 + (dynamic)v2);
        }
    }

    public struct MultiplyOp<T> : IOp<T>
    {
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

    public struct SubtractOp<T> : IOp<T>
    {
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

    public struct SubtractReverseOp<T> : IOp<T>
    {
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

    public struct DivideOp<T> : IOp<T>
    {
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

    public struct DivideReverseOp<T> : IOp<T>
    {
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

    public struct ModuloOp<T> : IOp<T>
    {
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

    public struct ModuloReverseOp<T> : IOp<T>
    {
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

    public struct NegateOp<T> : IOp<T>
    {
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

    public struct PlusOp<T> : IOp<T>

    {
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

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}