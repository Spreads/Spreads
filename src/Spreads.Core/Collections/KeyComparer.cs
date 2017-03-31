// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Experimantal
{
    internal class KeyComparer
    {
        internal static ConstrainedKeyComparer<T> CreateConstrained<T>() where T : IComparable<T>
        {
            return ConstrainedKeyComparer<T>.Default;
        }

        public static object Create(Type type)
        {
            var method = typeof(KeyComparer).GetTypeInfo().GetMethod("CreateConstrained", BindingFlags.NonPublic | BindingFlags.Static);
            var generic = method.MakeGenericMethod(type);
            var comparer = generic.Invoke(null, null);
            return comparer;
        }
    }

    public class KeyComparer<T> : IComparer<T>
    {
        private readonly IComparer<T> _comparer;

        // Set this to ConstrainedKeyComparer<T> if T implements IComparable<T>,
        // otherwise fallback to default IComparer or a provided one (not implemented here)
        public static KeyComparer<T> Default = Create();

        public KeyComparer() : this(null)
        {
        }

        public KeyComparer(IComparer<T> comparer)
        {
            _comparer = comparer ?? Comparer<T>.Default;
        }

        public virtual int Compare(T x, T y)
        {
            return _comparer.Compare(x, y);
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

        public virtual bool IsDiffable => false;

        private sealed class ConstrainedKeyComparerLong : KeyComparer<long>
        {

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(long x, long y)
            {
                return x.CompareTo(y);
            }

            public override bool IsDiffable => true;
        }

        private sealed class ConstrainedKeyComparerULong : KeyComparer<ulong>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(ulong x, ulong y)
            {
                return x.CompareTo(y);
            }

            public override bool IsDiffable => true;
        }

        private sealed class ConstrainedKeyComparerInt : KeyComparer<int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(int x, int y)
            {
                return x.CompareTo(y);
            }

            public override bool IsDiffable => true;
        }

        private sealed class ConstrainedKeyComparerUInt : KeyComparer<uint>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(uint x, uint y)
            {
                return x.CompareTo(y);
            }

            public override bool IsDiffable => true;
        }

        internal sealed class ConstrainedKeyComparerDateTime : KeyComparer<DateTime>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Compare(DateTime x, DateTime y)
            {
                return x.CompareTo(y);
            }

            public override bool IsDiffable => true;
        }
    }

    internal sealed class ConstrainedKeyComparer<T> : KeyComparer<T> where T : IComparable<T>
    {
        public static ConstrainedKeyComparer<T> Default = new ConstrainedKeyComparer<T>();

        private ConstrainedKeyComparer() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }
    }

    
}