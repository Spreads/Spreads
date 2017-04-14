// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Spreads.Collections;

namespace Spreads.Collections.Experimental
{
    // TODO Add/diff methods for primitives

    /// <summary>
    /// Fast IComparer implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class KeyComparer<T> : IKeyComparer<T>
    {
        // Set this to ConstrainedKeyComparer<T> if T implements IComparable<T>,
        // otherwise fallback to default IComparer or a provided one (not implemented here)

        /// <summary>
        /// Default instance of a KeyComparer for type T.
        /// </summary>
        public static KeyComparer<T> Default = CreateInstance();

        private readonly IComparer<T> _comparer;
        private readonly IKeyComparer<T> _keyComparer;

        internal KeyComparer() : this(null) { }

        /// <summary>
        ///
        /// </summary>
        /// <param name="comparer"></param>
        internal KeyComparer(IComparer<T> comparer)
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
        /// True if type T support Diff/Add methods
        /// </summary>
        public virtual bool IsDiffable => _keyComparer?.IsDiffable ?? false;

        /// <summary>
        /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
        /// </summary>
        public virtual T Add(T value, long diff)
        {
            if (_keyComparer != null)
            {
                _keyComparer.Add(value, diff);
            }
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public virtual int Compare(T x, T y)
        {
            return _comparer.Compare(x, y);
        }

        /// <summary>
        /// Returns Int64 distance between two values.
        /// </summary>
        public virtual long Diff(T x, T y)
        {
            if (_keyComparer != null)
            {
                return _keyComparer.Diff(x, y);
            }
            throw new NotSupportedException();
        }

        private static KeyComparer<T> CreateInstance()
        {
            if (typeof(T) == typeof(long))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerLong());
            }

            if (typeof(T) == typeof(ulong))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerULong());
            }

            if (typeof(T) == typeof(int))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerInt());
            }
            if (typeof(T) == typeof(uint))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerUInt());
            }
            if (typeof(T) == typeof(DateTime))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerDateTime());
            }

            // TODO IDiffable : IComparable
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return (KeyComparer<T>)KeyComparer.Create(typeof(T));
            }

            return new KeyComparer<T>(Comparer<T>.Default);
        }

        #region Implementations for primitive types

        internal sealed class ConstrainedKeyComparerDateTime : KeyComparer<DateTime>
        {
            // TODO
            public override bool IsDiffable => false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(DateTime x, DateTime y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerInt : KeyComparer<int>
        {
            // TODO
            public override bool IsDiffable => false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(int x, int y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerLong : KeyComparer<long>
        {
            // TODO
            public override bool IsDiffable => false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(long x, long y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerUInt : KeyComparer<uint>
        {
            // TODO
            public override bool IsDiffable => false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(uint x, uint y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerULong : KeyComparer<ulong>
        {
            // TODO
            public override bool IsDiffable => false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(ulong x, ulong y)
            {
                return x.CompareTo(y);
            }
        }
        #endregion Implementations for primitive types
    }

    internal sealed class ConstrainedKeyComparer<T> : KeyComparer<T> where T : IComparable<T>
    {
        public new static ConstrainedKeyComparer<T> Default = new ConstrainedKeyComparer<T>();

        private ConstrainedKeyComparer()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(T x, T y)
        {
            // ReSharper disable once PossibleNullReferenceException
            return x.CompareTo(y);
        }
    }

    internal class KeyComparer
    {
        public static object Create(Type type)
        {
            var method = typeof(KeyComparer).GetTypeInfo().GetMethod("CreateConstrained", BindingFlags.NonPublic | BindingFlags.Static);
            var generic = method.MakeGenericMethod(type);
            var comparer = generic.Invoke(null, null);
            return comparer;
        }

        internal static ConstrainedKeyComparer<T> CreateConstrained<T>() where T : IComparable<T>
        {
            return ConstrainedKeyComparer<T>.Default;
        }
    }
}