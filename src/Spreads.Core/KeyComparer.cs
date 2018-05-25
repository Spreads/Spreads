// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
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

        public int Compare(T x, T y)
        {
            return _comparer.Compare(x, y);
        }

        public bool IsDiffable => false;

        public T Add(T value, long diff)
        {
            ThrowHelper.ThrowNotSupportedException();
            return default;
        }

        public long Diff(T x, T y)
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
        private static readonly bool IsIComparable = typeof(IComparable<T>).GetTypeInfo().IsAssignableFrom(typeof(T));
        private readonly IKeyComparer<T> _keyComparer;

        /// <summary>
        /// Create a new KeyComparer instance.
        /// </summary>
        // private KeyComparer() : this(null) { }

        private KeyComparer(IComparer<T> comparer)
        {
            if (comparer != null && !ReferenceEquals(comparer, Comparer<T>.Default))
            {
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
        public static readonly KeyComparer<T> Default = new KeyComparer<T>();

        /// <summary>
        /// True if type T support <see cref="Diff"/> and <see cref="Add"/> methods.
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

                if (typeof(T) == typeof(DateTime))
                {
                    return true;
                }

                if (typeof(T) == typeof(long))
                {
                    return true;
                }

                if (typeof(T) == typeof(ulong))
                {
                    return true;
                }

                if (typeof(T) == typeof(int))
                {
                    return true;
                }

                if (typeof(T) == typeof(uint))
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
        public static KeyComparer<T> Create(IKeyComparer<T> comparer)
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
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Add(T value, long diff)
        {
            if (_keyComparer != null)
            {
                if (!_keyComparer.IsDiffable)
                {
                    ThrowHelper.ThrowInvalidOperationException("Cannot Add: KeyComparer.IsDiffable is false");
                }
                return _keyComparer.Add(value, diff);
            }

            if (typeof(T) == typeof(DateTime))
            {
                var value1 = (DateTime)(object)(value);
                return (T)(object)value1.AddTicks(diff);
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

            ThrowHelper.ThrowNotSupportedException();
            return default(T);
        }

        /// <inheritdoc />
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            if (_keyComparer != null)
            {
                return _keyComparer.Compare(x, y);
            }

            if (typeof(T) == typeof(DateTime))
            {
                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);

                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(long))
            {
                var x1 = (long)(object)(x);
                var y1 = (long)(object)(y);

                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(ulong))
            {
                var x1 = (ulong)(object)(x);
                var y1 = (ulong)(object)(y);
                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(int))
            {
                var x1 = (int)(object)(x);
                var y1 = (int)(object)(y);
                return x1.CompareTo(y1);
            }

            if (typeof(T) == typeof(uint))
            {
                var x1 = (uint)(object)(x);
                var y1 = (uint)(object)(y);

                return x1.CompareTo(y1);
            }

            // NB all primitive types are IComparable, all custom types could be easily made such
            // This optimization using Spreads.Unsafe package works for any type that implements
            // the interface and is as fast as `typeof(T) == typeof(...)` approach.
            // The special cases above are left for scenarios when the "static readonly" optimization
            // doesn't work, e.g. AOT. See discussion #100.
            if (IsIComparable)
            {
                return Unsafe.CompareToConstrained(ref x, ref y);
            }

            return CompareSlow(x, y);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int CompareSlow(T x, T y)
        {
            return Comparer<T>.Default.Compare(x, y);
        }

        /// <summary>
        /// Returns Int64 distance between two values.
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Diff(T x, T y)
        {
            if (_keyComparer != null)
            {
                if (!_keyComparer.IsDiffable)
                {
                    ThrowHelper.ThrowInvalidOperationException("Cannot Diff: KeyComparer.IsDiffable is false");
                }
                return _keyComparer.Diff(x, y);
            }

            if (typeof(T) == typeof(DateTime))
            {
                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);

                return checked(x1.Ticks - y1.Ticks);
            }

            if (typeof(T) == typeof(long))
            {
                var x1 = (long)(object)(x);
                var y1 = (long)(object)(y);

                return checked(x1 - y1);
            }

            if (typeof(T) == typeof(ulong))
            {
                var x1 = (ulong)(object)(x);
                var y1 = (ulong)(object)(y);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(int))
            {
                var x1 = (int)(object)(x);
                var y1 = (int)(object)(y);
                return checked((long)(x1) - (long)y1);
            }

            if (typeof(T) == typeof(uint))
            {
                var x1 = (uint)(object)(x);
                var y1 = (uint)(object)(y);
                return checked((long)(x1) - y1);
            }

            ThrowHelper.ThrowNotSupportedException();
            return 0L;
        }

        /// <inheritdoc />
        [Pure]
        public bool Equals(T x, T y)
        {
            if (_keyComparer != null)
            {
                return _keyComparer.Compare(x, y) == 0;
            }

            // ReSharper disable PossibleNullReferenceException
            if (typeof(T) == typeof(DateTime))
            {
                // TODO (low) unsafe impl with bitwise sign
                var x1 = (DateTime)(object)(x);
                var y1 = (DateTime)(object)(y);
                return x1 == y1;
            }

            if (typeof(T) == typeof(long))
            {
                // TODO (low) unsafe impl with bitwise sign
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
            // ReSharper restore PossibleNullReferenceException
            return EqualityComparer<T>.Default.Equals(x, y);
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

                int c = Compare(value, System.Runtime.CompilerServices.Unsafe.Add(ref start, i));
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