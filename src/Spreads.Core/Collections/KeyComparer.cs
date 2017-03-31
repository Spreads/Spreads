// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Experimantal
{
    public class KeyComparer
    {
        public static ConstrainedKeyComparer<T> CreateConstrained<T>() where T : IComparable<T>
        {
            return new ConstrainedKeyComparer<T>();
        }

        public static object Create(Type type)
        {
            var method = typeof(KeyComparer).GetTypeInfo().GetMethod("CreateConstrained");
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
        public static KeyComparer<T> Instance = Create();

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
            if (typeof(IComparable<T>).IsAssignableFrom(ty))
            {
                return (KeyComparer<T>)KeyComparer.Create(typeof(T));
            }

            return new KeyComparer<T>(Comparer<T>.Default);
        }
    }

    public sealed class ConstrainedKeyComparer<T> : KeyComparer<T> where T : IComparable<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }
    }
}