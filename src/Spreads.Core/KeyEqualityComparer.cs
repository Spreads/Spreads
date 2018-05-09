// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads
{
    // NB Any type could be wrapped into a struct that implements IEquatable<T> in any custom way
    // TODO1 remove IComparer support from KeyComparer, the null check is quite visible
    // TODO2 add static methods in Comparer like here, use them from containers
    // TODO3 with NB above, remove support for IComparer completely, struct wrappers is the
    //       right approach if ever custom comparer is needed. Add wrapper implementations
    //       for strings, e.g. OrdinalIgnoreCase, etc

    /// <summary>
    /// Fast IEqualityComparer implementation that only supports IEquatable types or default equality comparison.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct KeyEqualityComparer<T> : IEqualityComparer<T>
    {
        private static readonly KeyEqualityComparer<T> _default = new KeyEqualityComparer<T>();
        private static readonly bool IsIEquatable = typeof(IEquatable<T>).GetTypeInfo().IsAssignableFrom(typeof(T));

        /// <summary>
        /// Create a new KeyEqualityComparer instance.
        /// </summary>
        //private KeyEqualityComparer() { }

        /// <summary>
        /// Default instance of a KeyEqualityComparer for type T.
        /// </summary>
        public static KeyEqualityComparer<T> Default => _default;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T x, T y)
        {
            return EqualsStatic(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool EqualsStatic(T x, T y)
        {
            if (IsIEquatable)
            {
                return Unsafe.EqualsConstrained(ref x, ref y);
            }

            return EqualityComparer<T>.Default.Equals(x, y);
        }

        /// <summary>
        /// GetHashCode is not supported.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(T x)
        {
            return GetHashCodeStatic(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetHashCodeStatic(T x)
        {
            if (IsIEquatable)
            {
                return Unsafe.GetHashCodeConstrained(ref x);
            }

            return EqualityComparer<T>.Default.GetHashCode(x);
        }
    }
}