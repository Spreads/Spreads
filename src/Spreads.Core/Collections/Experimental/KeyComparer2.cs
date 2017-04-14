// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Experimental
{
    /// <summary>
    /// IComparer'T with optional Add/Diff methods.
    /// </summary>
    public interface IKeyComparer<T> : IComparer<T>
    {
        /// <summary>
        /// True is Add/Diff methods are supported.
        /// </summary>
        bool IsDiffable { get; }

        /// <summary>
        /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
        /// </summary>
        T Add(T value, long diff);

        /// <summary>
        /// Returns int64 distance between two values when they are stored in
        /// a regular sorted map. Regular means continuous integers or days or seconds, etc.
        /// </summary>
        /// <remarks>
        /// This method could be used for IComparer'T.Compare implementation,
        /// but must be checked for int overflow (e.g. compare Diff result to 0L instead of int cast).
        /// </remarks>
        long Diff(T x, T y);
    }

    /// <summary>
    /// Fast IComparer implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class KeyComparer<T> : IKeyComparer<T>
    {
        private static readonly KeyComparer<T> _default = new KeyComparer<T>();
        private readonly IComparer<T> _comparer;
        private readonly IKeyComparer<T> _keyComparer;

        /// <summary>
        /// Create a new KeyComparer instance.
        /// </summary>
        private KeyComparer() : this(null) { }

        private KeyComparer(IComparer<T> comparer)
        {
            if (comparer != null)
            {
                _comparer = comparer;
                if (comparer is IKeyComparer<T> kc)
                {
                    _keyComparer = kc;
                }
            }
        }

        public static KeyComparer<T> Default => _default;

        /// <summary>
        /// True if type T support Diff/Add methods
        /// </summary>
        public bool IsDiffable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_keyComparer != null)
                {
                    return _keyComparer.IsDiffable;
                }

                var ty = typeof(T);

                if (ty == typeof(DateTime))
                {
                    return true;
                }

                if (ty == typeof(long))
                {
                    return true;
                }

                if (ty == typeof(ulong))
                {
                    return true;
                }

                if (ty == typeof(int))
                {
                    return true;
                }

                if (ty == typeof(uint))
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Get a new or default instance of KeyComparer.
        /// </summary>
        public static KeyComparer<T> Create(IComparer<T> comparer = null)
        {
            if (comparer == null || ReferenceEquals(comparer, Comparer<T>.Default))
            {
                return Default;
            }
            return new KeyComparer<T>(comparer);
        }

        /// <summary>
        /// Get a new or default instance of KeyComparer.
        /// </summary>
        public static KeyComparer<T> Create(IKeyComparer<T> comparer)
        {
            return comparer == null ? Default : new KeyComparer<T>(comparer);
        }

        /// <summary>
        /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Add(T value, long diff)
        {
            if (_keyComparer != null)
            {
                return _keyComparer.Add(value, diff);
            }

            var ty = typeof(T);

            if (ty == typeof(DateTime))
            {
                var value1 = (DateTime)(object)(value);
                return (T)(object)value1.AddTicks(diff);
            }

            if (ty == typeof(long))
            {
                var value1 = (long)(object)(value);
                return (T)(object)(checked(value1 + diff));
            }

            if (ty == typeof(ulong))
            {
                var value1 = (ulong)(object)(value);
                return (T)(object)(checked((long)value1 + diff));
            }

            if (ty == typeof(int))
            {
                var value1 = (int)(object)(value);
                return (T)(object)(checked(value1 + diff));
            }

            if (ty == typeof(uint))
            {
                var value1 = (uint)(object)(value);
                return (T)(object)(checked((int)value1 + diff));
            }

            throw new NotSupportedException();
        }

        /// <inheritdoc />
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            if (_comparer != null)
            {
                return _comparer.Compare(x, y);
            }

            var ty = typeof(T);

            if (ty == typeof(DateTime))
            {
                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);

                if (x1 < y1) return -1;
                if (x1 > y1) return 1;
                return 0;
            }

            if (ty == typeof(long))
            {
                var x1 = (long)(object)(x);
                var y1 = (long)(object)(y);

                // Need to use compare because subtraction will wrap
                // to positive for very large neg numbers, etc.
                if (x1 < y1) return -1;
                if (x1 > y1) return 1;
                return 0;
            }

            if (ty == typeof(ulong))
            {
                var x1 = (ulong)(object)(x);
                var y1 = (ulong)(object)(y);

                if (x1 < y1) return -1;
                if (x1 > y1) return 1;
                return 0;
            }

            if (ty == typeof(int))
            {
                var x1 = (int)(object)(x);
                var y1 = (int)(object)(y);
                return x1 - y1;
            }

            if (ty == typeof(uint))
            {
                var x1 = (uint)(object)(x);
                var y1 = (uint)(object)(y);

                if (x1 < y1) return -1;
                if (x1 > y1) return 1;
                return 0;
            }

            return Comparer<T>.Default.Compare(x, y);
        }

        /// <summary>
        /// Returns Int64 distance between two values.
        /// </summary>
        /// <remarks>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Diff(T x, T y)
        {
            if (_keyComparer != null)
            {
                return _keyComparer.Diff(x, y);
            }

            var ty = typeof(T);

            if (ty == typeof(DateTime))
            {
                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);

                return x1.Ticks - y1.Ticks;
            }

            if (ty == typeof(long))
            {
                var x1 = (long)(object)(x);
                var y1 = (long)(object)(y);

                return x1 - y1;
            }

            if (ty == typeof(ulong))
            {
                var x1 = (ulong)(object)(x);
                var y1 = (ulong)(object)(y);
                return checked((long)(x1) - (long)y1);
            }

            if (ty == typeof(int))
            {
                var x1 = (int)(object)(x);
                var y1 = (int)(object)(y);
                return x1 - y1;
            }

            if (ty == typeof(uint))
            {
                var x1 = (uint)(object)(x);
                var y1 = (uint)(object)(y);

                return checked((long)(x1) - (long)y1);
            }

            throw new NotSupportedException();
        }
    }

}