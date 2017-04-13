// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Experimantal
{

    // TODO Add/diff methods for primitives

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
    public class KeyComparer<T> : IKeyComparer<T>
    {
        // Set this to ConstrainedKeyComparer<T> if T implements IComparable<T>,
        // otherwise fallback to default IComparer or a provided one (not implemented here)
        public static KeyComparer<T> Default = Create();

        private readonly IComparer<T> _comparer;
        public KeyComparer() : this(null)
        {
        }

        public KeyComparer(IComparer<T> comparer)
        {
            _comparer = comparer ?? Comparer<T>.Default;
        }

        public virtual bool IsDiffable => false;

        /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
        public virtual T Add(T value, long diff)
        {
            throw new NotSupportedException();
        }

        public virtual int Compare(T x, T y)
        {
            return _comparer.Compare(x, y);
        }

        /// Returns int64 distance between two values when they are stored in
        /// a regular sorted map. Regular means continuous integers or days or seconds, etc.
        /// ## Remarks
        /// This method could be used for IComparer<'K>.Compare implementation,
        /// but must be checked for int overflow (e.g. compare Diff result to 0L instead of int cast).
        public virtual long Diff(T x, T y)
        {
            throw new NotSupportedException();
        }

        private static KeyComparer<T> Create()
        {
            var ty = typeof(T);
            if (ty == typeof(long))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerLong());
            }

            if (ty == typeof(ulong))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerULong());
            }

            if (ty == typeof(int))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerInt());
            }
            if (ty == typeof(uint))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerUInt());
            }
            if (ty == typeof(DateTime))
            {
                return (KeyComparer<T>)(object)(new ConstrainedKeyComparerDateTime());
            }

            if (typeof(IComparable<T>).IsAssignableFrom(ty))
            {
                return (KeyComparer<T>)KeyComparer.Create(typeof(T));
            }

            return new KeyComparer<T>(Comparer<T>.Default);
        }
        #region Implementations for primitive types

        internal sealed class ConstrainedKeyComparerDateTime : KeyComparer<DateTime>
        {
            public override bool IsDiffable => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(DateTime x, DateTime y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerInt : KeyComparer<int>
        {
            public override bool IsDiffable => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(int x, int y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerLong : KeyComparer<long>
        {
            public override bool IsDiffable => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(long x, long y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerUInt : KeyComparer<uint>
        {
            public override bool IsDiffable => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(uint x, uint y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class ConstrainedKeyComparerULong : KeyComparer<ulong>
        {
            public override bool IsDiffable => true;

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
        public static ConstrainedKeyComparer<T> Default = new ConstrainedKeyComparer<T>();

        private ConstrainedKeyComparer()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(T x, T y)
        {
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