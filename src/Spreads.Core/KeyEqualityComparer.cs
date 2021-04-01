﻿// This Source Code Form is subject to the terms of the Mozilla Public
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
        private static readonly bool _isIEquatable = typeof(IEquatable<T>).GetTypeInfo().IsAssignableFrom(typeof(T));

        /// <summary>
        /// Binary instance of a KeyEqualityComparer for type T.
        /// </summary>
        public static KeyEqualityComparer<T> Default { get; } = new KeyEqualityComparer<T>();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T x, T y)
        {
            return EqualsImpl(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool EqualsImpl(T x, T y)
        {
            return _isIEquatable
                ? UnsafeEx.EqualsConstrained(ref x, ref y)
                : EqualityComparer<T>.Default.Equals(x, y);

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
            return _isIEquatable
                ? UnsafeEx.GetHashCodeConstrained(ref x)
                : EqualityComparer<T>.Default.GetHashCode(x);
        }
    }
}
