// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using Spreads.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads
{
    internal class DummyKeyComparer<T> : IKeyComparer<T>
    {
        private IComparer<T> _comparer;

        public DummyKeyComparer(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        [Pure]
        public int Compare(T x, T y)
        {
            return _comparer.Compare(x, y);
        }

        public bool IsDiffable => false;

        [Pure]
        public T Add(T value, long diff)
        {
            ThrowHelper.ThrowNotSupportedException();
            return default;
        }

        [Pure]
        public long Diff(T minuend, T subtrahend)
        {
            ThrowHelper.ThrowNotSupportedException();
            return default;
        }
    }

    /// <summary>
    /// Fast IComparer implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public readonly struct KeyComparer<T> : IKeyComparer<T>, IEqualityComparer<T>, IEquatable<KeyComparer<T>>
    {
        /// <summary>
        /// Returns true for built-in types that are numbers or could be represented as numbers via <see cref="IInt64Diffable{T}"/>.
        /// </summary>
        public static readonly bool IsBuiltInNumericType =
            typeof(T) == typeof(bool)
            || typeof(T) == typeof(byte)
            || typeof(T) == typeof(sbyte)
            || typeof(T) == typeof(char)
            || typeof(T) == typeof(short)
            || typeof(T) == typeof(ushort)
            || typeof(T) == typeof(int)
            || typeof(T) == typeof(uint)
            || typeof(T) == typeof(long)
            || typeof(T) == typeof(ulong)
            || typeof(T) == typeof(float)
            || typeof(T) == typeof(double)
            || typeof(T) == typeof(decimal)
            || typeof(T) == typeof(DateTime)
            || typeof(T) == typeof(Timestamp);

        /// <summary>
        /// Returns true for types that implement <see cref="IComparable{T}"/> interface.
        /// </summary>
        public static readonly bool IsIComparable = typeof(IComparable<T>).GetTypeInfo().IsAssignableFrom(typeof(T));

        /// <summary>
        /// Returns true for types that implement <see cref="IInt64Diffable{T}"/> interface.
        /// </summary>
        private static readonly bool IsIInt64Diffable = typeof(IInt64Diffable<T>).GetTypeInfo().IsAssignableFrom(typeof(T));

        private readonly IKeyComparer<T> _keyComparer;

        private KeyComparer(IComparer<T> comparer)
        {
            if (comparer != null && !ReferenceEquals(comparer, Comparer<T>.Default))
            {
                //
                if (IsBuiltInNumericType)
                {
                    ThrowHelper.ThrowNotSupportedException("Custom IComparer<T> for built-in type is not supported. Create a wrapper struct that implements IComparable<T> and use KeyComparer<T>.Default for it.");
                }
                if (IsIComparable)
                {
                    ThrowHelper.ThrowNotSupportedException("Custom IComparer<T> for a type T that implements IComparable<T> is not supported. Create a wrapper struct that implements a different IComparable<T> logic and use KeyComparer<T>.Default for it.");
                }
                if (comparer is IKeyComparer<T> kc)
                {
                    _keyComparer = kc;
                }
                else
                {
                    _keyComparer = new DummyKeyComparer<T>(comparer);
                }
            }
            else
            {
                _keyComparer = null;
            }
        }

        /// <summary>
        /// Binary instance of a KeyComparer for type T.
        /// </summary>
        public static readonly KeyComparer<T> Default = default;

        /// <summary>
        /// Returns true for types that implement <see cref="IInt64Diffable{T}"/> interface or are <see cref="IsBuiltInNumericType"/>.
        /// </summary>
        public bool IsDiffable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ( // no bool
                    typeof(T) == typeof(byte)
                    || typeof(T) == typeof(sbyte)
                    || typeof(T) == typeof(char)
                    || typeof(T) == typeof(short)
                    || typeof(T) == typeof(ushort)
                    || typeof(T) == typeof(int)
                    || typeof(T) == typeof(uint)
                    || typeof(T) == typeof(long)
                    || typeof(T) == typeof(ulong)
                    // || typeof(T) == typeof(float) // could lose precision for floats and decimals
                    // || typeof(T) == typeof(double)
                    // || typeof(T) == typeof(decimal)
                    || typeof(T) == typeof(DateTime)
                    || typeof(T) == typeof(Timestamp)
                    // custom Spreads types below to completely JIT-eliminate this call
                    // || typeof(T) == typeof(SmallDecimal) // TODO
                    )
                {
                    return true;
                }

                if (IsIInt64Diffable)
                {
                    return true;
                }

                if (_keyComparer != null)
                {
                    return _keyComparer.IsDiffable;
                }

                return false;
            }
        }

        /// <summary>
        /// True is we could safely prefer interpolation search over binary search. Mostly known types for now.
        /// </summary>
        public static bool IsDiffableSafe
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ( // no bool
                    typeof(T) == typeof(byte)
                    || typeof(T) == typeof(sbyte)
                    || typeof(T) == typeof(char)
                    || typeof(T) == typeof(short)
                    || typeof(T) == typeof(ushort)
                    || typeof(T) == typeof(int)
                    || typeof(T) == typeof(uint)
                    || typeof(T) == typeof(long)
                    || typeof(T) == typeof(ulong)
                    // || typeof(T) == typeof(float) // could lose precision for floats and decimals
                    // || typeof(T) == typeof(double)
                    // || typeof(T) == typeof(decimal)
                    || typeof(T) == typeof(DateTime)
                    || typeof(T) == typeof(Timestamp)
                // custom Spreads types below to completely JIT-eliminate this call
                // || typeof(T) == typeof(SmallDecimal) // TODO
                )
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

            if (comparer is KeyComparer<T> kc)
            {
                return kc;
            }

            return new KeyComparer<T>(comparer);
        }

        /// <summary>
        /// Get a new or default instance of KeyComparer.
        /// </summary>
        public static KeyComparer<T> Create(IKeyComparer<T> comparer = null)
        {
            if (comparer == null)
            {
                return Default;
            }
            if (comparer is KeyComparer<T> kc)
            {
                return kc;
            }
            return new KeyComparer<T>(comparer);
        }

        /// <inheritdoc />
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            // For our purposes this method is "normal", i.e. known types are compared normally
            // TODO (docs) It is easy to compare known types in a custom way - just implement a wrapper struct and IComparable<T>. This also will be faster than a comparable.

            if (typeof(T) == typeof(bool))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (bool)(object)(x);
                var y1 = (bool)(object)(y);
                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(byte))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (byte)(object)(x);
                var y1 = (byte)(object)(y);
                return x1 - y1;
            }

            if (typeof(T) == typeof(sbyte))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (sbyte)(object)(x);
                var y1 = (sbyte)(object)(y);
                return x1 - y1;
            }

            if (typeof(T) == typeof(char))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (char)(object)(x);
                var y1 = (char)(object)(y);
                return x1 - y1;
            }

            if (typeof(T) == typeof(short))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (short)(object)(x);
                var y1 = (short)(object)(y);
                return x1 - y1;
            }

            if (typeof(T) == typeof(ushort))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (ushort)(object)(x);
                var y1 = (ushort)(object)(y);

                // ReSharper disable RedundantCast
                return (int)x1 - (int)y1;
                // ReSharper restore RedundantCast
            }

            if (typeof(T) == typeof(int))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (int)(object)(x);
                var y1 = (int)(object)(y);
                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(uint))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (uint)(object)(x);
                var y1 = (uint)(object)(y);

                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(long))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (long)(object)(x);
                var y1 = (long)(object)(y);

                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(ulong))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (ulong)(object)(x);
                var y1 = (ulong)(object)(y);
                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(float))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (float)(object)(x);
                var y1 = (float)(object)(y);

                if (x1 < y1) { return -1; }
                if (x1 > y1) { return 1; }
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (x1 == y1) { return 0; }

                // At least one of the values is NaN.
                if (float.IsNaN(x1))
                { return (float.IsNaN(y1) ? 0 : -1); }
                else // f is NaN.
                { return 1; }
            }

            if (typeof(T) == typeof(double))
            {
                // x1.CompareTo(y1) is not inlined, copy manually
                // https://github.com/dotnet/corefx/blob/5fe165ab631675273f5d19bebc15b5733ef1354d/src/Common/src/CoreLib/System/Double.cs#L147
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (double)(object)(x);
                var y1 = (double)(object)(y);

                if (x1 < y1) { return -1; }
                if (x1 > y1) { return 1; }
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (x1 == y1) { return 0; }

                // At least one of the values is NaN.
                if (double.IsNaN(x1))
                { return (double.IsNaN(y1) ? 0 : -1); }
                else
                { return 1; }
            }

            if (typeof(T) == typeof(decimal))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (decimal)(object)(x);
                var y1 = (decimal)(object)(y);

                return decimal.Compare(x1, y1);
            }

            if (typeof(T) == typeof(DateTime))
            {
                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);
                return DateTime.Compare(x1, y1);
            }

            if (typeof(T) == typeof(Timestamp))
            {
                // TODO for TS we could use sub in normal cases

                Debug.Assert(_keyComparer == null, "Known types should not have a comparer");

                var x1 = ((Timestamp)(object)(x)).Nanos;
                var y1 = ((Timestamp)(object)(y)).Nanos;

                return x1.CompareTo(y1);
            }

            // NB all primitive types are IComparable, all custom types could be easily made such
            // This optimization using Spreads.Unsafe package works for any type that implements
            // the interface and is as fast as `typeof(T) == typeof(...)` approach.
            // The special cases above are left for scenarios when the "static readonly" optimization
            // doesn't work, e.g. AOT. See discussion #100.
            if (IsIComparable)
            {
                return UnsafeEx.CompareToConstrained(ref x, ref y);
            }

            if (_keyComparer != null)
            {
                return _keyComparer.Compare(x, y);
            }

            return CompareSlow(x, y);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CompareSlow(T x, T y)
        {
            return Comparer<T>.Default.Compare(x, y);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T AddViaInterface(T value, long diff)
        {
            if (!_keyComparer.IsDiffable)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot Add: KeyComparer.IsDiffable is false");
            }
            return _keyComparer.Add(value, diff);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Add(T value, long diff)
        {
            if (typeof(T) == typeof(byte))
            {
                var value1 = (byte)(object)(value);
                return (T)(object)(checked((byte)((sbyte)value1 + diff)));
            }

            if (typeof(T) == typeof(sbyte))
            {
                var value1 = (sbyte)(object)(value);
                return (T)(object)(checked((sbyte)(value1 + diff)));
            }

            if (typeof(T) == typeof(char))
            {
                var value1 = (char)(object)(value);
                return (T)(object)(checked((char)(value1 + diff)));
            }

            if (typeof(T) == typeof(short))
            {
                var value1 = (short)(object)(value);
                return (T)(object)(checked((short)(value1 + diff)));
            }

            if (typeof(T) == typeof(ushort))
            {
                var value1 = (ushort)(object)(value);
                return (T)(object)(checked((ushort)((short)value1 + diff)));
            }

            if (typeof(T) == typeof(int))
            {
                var value1 = (int)(object)(value);
                return (T)(object)(checked((int)(value1 + diff)));
            }

            if (typeof(T) == typeof(uint))
            {
                var value1 = (uint)(object)(value);
                return (T)(object)(checked((uint)((int)value1 + diff)));
            }

            if (typeof(T) == typeof(long))
            {
                var value1 = (long)(object)(value);
                return (T)(object)(checked(value1 + diff));
            }

            if (typeof(T) == typeof(ulong))
            {
                var value1 = (ulong)(object)(value);
                return (T)(object)(checked((ulong)((long)value1 + diff)));
            }

            if (typeof(T) == typeof(DateTime))
            {
                var value1 = (DateTime)(object)(value);
                return (T)(object)value1.AddTicks(diff);
            }

            if (typeof(T) == typeof(Timestamp))
            {
                var value1 = (Timestamp)(object)(value);
                return (T)(object)(new Timestamp(value1.Nanos + diff));
            }

            // TODO SmallDecimal

            if (IsIInt64Diffable)
            {
                return UnsafeEx.AddLongConstrained(ref value, diff);
            }

            if (_keyComparer != null)
            {
                return AddViaInterface(value, diff);
            }

            ThrowHelper.ThrowNotSupportedException();
            return default(T);
        }

        /// <summary>
        /// Returns Int64 distance between two values.
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Diff(T minuend, T subtrahend)
        {
            // ReSharper disable RedundantCast
            if (typeof(T) == typeof(byte))
            {
                var x1 = (byte)(object)(minuend);
                var y1 = (byte)(object)(subtrahend);

                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(sbyte))
            {
                var x1 = (sbyte)(object)(minuend);
                var y1 = (sbyte)(object)(subtrahend);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(char))
            {
                var x1 = (char)(object)(minuend);
                var y1 = (char)(object)(subtrahend);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(short))
            {
                var x1 = (short)(object)(minuend);
                var y1 = (short)(object)(subtrahend);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(ushort))
            {
                var x1 = (ushort)(object)(minuend);
                var y1 = (ushort)(object)(subtrahend);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(int))
            {
                var x1 = (int)(object)(minuend);
                var y1 = (int)(object)(subtrahend);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(uint))
            {
                var x1 = (uint)(object)(minuend);
                var y1 = (uint)(object)(subtrahend);
                return checked((long)(x1) - y1);
            }

            if (typeof(T) == typeof(long))
            {
                var x1 = (long)(object)(minuend);
                var y1 = (long)(object)(subtrahend);

                return checked(x1 - y1);
            }

            if (typeof(T) == typeof(ulong))
            {
                var x1 = (ulong)(object)(minuend);
                var y1 = (ulong)(object)(subtrahend);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(DateTime))
            {
                var x1 = (DateTime)(object)(minuend);
                var y1 = (DateTime)(object)(subtrahend);

                return checked(x1.Ticks - y1.Ticks);
            }

            if (typeof(T) == typeof(Timestamp))
            {
                var x1 = (Timestamp)(object)(minuend);
                var y1 = (Timestamp)(object)(subtrahend);

                return checked(x1.Nanos - y1.Nanos);
            }

            // ReSharper restore RedundantCast
            // TODO SmallDecimal

            if (IsIInt64Diffable)
            {
                return UnsafeEx.DiffLongConstrained(ref minuend, ref subtrahend);
            }

            if (_keyComparer != null)
            {
                return DiffViaInterface(minuend, subtrahend);
            }

            ThrowHelper.ThrowNotSupportedException();
            return 0L;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private long DiffViaInterface(T minuend, T subtrahend)
        {
            if (!_keyComparer.IsDiffable)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot Diff: KeyComparer.IsDiffable is false");
            }

            return _keyComparer.Diff(minuend, subtrahend);
        }

        /// <inheritdoc />
        [Pure]
        public bool Equals(T x, T y)
        {
            // ReSharper disable PossibleNullReferenceException

            if (typeof(T) == typeof(bool))
            {
                var x1 = (bool)(object)(x);
                var y1 = (bool)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(byte))
            {
                var x1 = (byte)(object)(x);
                var y1 = (byte)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(sbyte))
            {
                var x1 = (sbyte)(object)(x);
                var y1 = (sbyte)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(char))
            {
                var x1 = (char)(object)(x);
                var y1 = (char)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(short))
            {
                var x1 = (short)(object)(x);
                var y1 = (short)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(ushort))
            {
                var x1 = (ushort)(object)(x);
                var y1 = (ushort)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(int))
            {
                var x1 = (int)(object)(x);
                var y1 = (int)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(uint))
            {
                var x1 = (uint)(object)(x);
                var y1 = (uint)(object)(y);

                return x1 == y1;
            }

            if (typeof(T) == typeof(long))
            {
                var x1 = (long)(object)(x);
                var y1 = (long)(object)(y);

                return x1 == y1;
            }

            if (typeof(T) == typeof(ulong))
            {
                var x1 = (ulong)(object)(x);
                var y1 = (ulong)(object)(y);

                return x1 == y1;
            }

            if (typeof(T) == typeof(decimal))
            {
                var x1 = (decimal)(object)(x);
                var y1 = (decimal)(object)(y);

                return x1 == y1;
            }
            if (typeof(T) == typeof(float))
            {
                var x1 = (float)(object)(x);
                var y1 = (float)(object)(y);

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return x1 == y1;
            }

            if (typeof(T) == typeof(double))
            {
                var x1 = (double)(object)(x);
                var y1 = (double)(object)(y);

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return x1 == y1;
            }
            if (typeof(T) == typeof(DateTime))
            {
                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(Timestamp))
            {
                var x1 = (Timestamp)(object)(x);
                var y1 = (Timestamp)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(SmallDecimal))
            {
                var x1 = (SmallDecimal)(object)(x);
                var y1 = (SmallDecimal)(object)(y);
                return x1 == y1;
            }

            return Compare(x, y) == 0;
        }

        /// <summary>
        /// GetHashCode is not supported.
        /// </summary>
        public int GetHashCode(T obj)
        {
            throw new NotSupportedException("KeyComparer should not be used for hash code calculations.");
        }

        public bool Equals(KeyComparer<T> other)
        {
            return Equals(_keyComparer, other._keyComparer);
        }

        public override bool Equals(object obj)
        {
            if (obj is KeyComparer<T> kc)
            {
                return Equals(kc);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_keyComparer != null ? _keyComparer.GetHashCode() : 0) * 397) ^ (_keyComparer != null ? _keyComparer.GetHashCode() : 0);
            }
        }

        [Obsolete("Use VectorSearch")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int BinarySearch(T[] array, int index, int length, T value)
        {
            // TODO review while rewriting SCM if this shortcut could be useful
            //var c0 = Compare(value, array[length - 1]);

            //if (c0 > 0)
            //{
            //    return ~length;
            //}

            //if (c0 == 0)
            //{
            //    return length - 1;
            //}

            // https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/SpanHelpers.BinarySearch.cs
            ref var start = ref array[0];
            int lo = 0;
            int hi = length - 1;

            // If length == 0, hi == -1, and loop will not be entered
            while (lo <= hi)
            {
                // PERF: `lo` or `hi` will never be negative inside the loop,
                //       so computing median using uints is safe since we know
                //       `length <= int.MaxValue`, and indices are >= 0
                //       and thus cannot overflow an uint.
                //       Saves one subtraction per loop compared to
                //       `int i = lo + ((hi - lo) >> 1);`
                int i = (int)(((uint)hi + (uint)lo) >> 1);

                int c = Compare(value, Unsafe.Add(ref start, i));
                if (c == 0)
                {
                    return i;
                }
                else if (c > 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }
            // If none found, then a negative number that is the bitwise complement
            // of the index of the next element that is larger than or, if there is
            // no larger element, the bitwise complement of `length`, which
            // is `lo` at this point.
            return ~lo;
        }
    }

    /// <summary>
    /// Fast IComparer for KeyValuePair.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public sealed class KVPComparer<TKey, TValue> : IComparer<KeyValuePair<TKey, TValue>>, IEqualityComparer<KeyValuePair<TKey, TValue>>, IEquatable<KVPComparer<TKey, TValue>>
    {
        private readonly KeyComparer<TKey> _keyComparer;
        private readonly KeyComparer<TValue> _valueComparer;

        /// <summary>
        /// Create a KVP comparer that compares keys and values.
        /// </summary>
        public KVPComparer(KeyComparer<TKey> keyComparer, KeyComparer<TValue> valueComparer)
        {
            _keyComparer = keyComparer.Equals(default(KeyComparer<TKey>)) ? KeyComparer<TKey>.Default : keyComparer;
            _valueComparer = valueComparer.Equals(default(KeyComparer<TValue>)) ? KeyComparer<TValue>.Default : valueComparer;
        }

        /// <summary>
        /// Create a KVP comparer that only compares keys. Pass null as a second constructor argument
        /// to use a default comparer for values. With this constructor values are ignored.
        /// </summary>
        public KVPComparer(KeyComparer<TKey> keyComparer)
        {
            _keyComparer = keyComparer.Equals(default(KeyComparer<TKey>)) ? KeyComparer<TKey>.Default : keyComparer;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
        {
            var c1 = _keyComparer.Compare(x.Key, y.Key);
            if (c1 == 0 && !_valueComparer.Equals(default(KeyComparer<TValue>)))
            {
                return _valueComparer.Compare(x.Value, y.Value);
            }
            return c1;
        }

        /// <inheritdoc />
        public bool Equals(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
        {
            return _keyComparer.Equals(x.Key, y.Key) && _valueComparer.Equals(default(KeyComparer<TValue>)) || _valueComparer.Equals(x.Value, y.Value);
        }

        /// <summary>
        /// GetHashCode is not supported.
        /// </summary>
        public int GetHashCode(KeyValuePair<TKey, TValue> obj)
        {
            // TODO (?)
            throw new NotSupportedException("KVPComparer should not be used for hash code calculations.");
        }

        public bool Equals(KVPComparer<TKey, TValue> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(_keyComparer, other._keyComparer) && Equals(_valueComparer, other._valueComparer);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is KVPComparer<TKey, TValue> && Equals((KVPComparer<TKey, TValue>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((!_keyComparer.Equals(default(KeyComparer<TKey>)) ? _keyComparer.GetHashCode() : 0) * 397) ^ (!_valueComparer.Equals(default(KeyComparer<TValue>)) ? _valueComparer.GetHashCode() : 0);
            }
        }
    }
}