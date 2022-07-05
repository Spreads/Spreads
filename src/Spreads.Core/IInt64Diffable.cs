// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads
{
    /// <summary>
    /// An interface for a type whose value could be stored as an Int64 delta from a previous value and recovered later by addition.
    /// </summary>
    /// <seealso cref="IDelta{T}"/>
    public interface IInt64Diffable<T> : IComparable<T>
    {
        /// <summary>
        /// Add <paramref name="diff"/> to this value as long.
        /// </summary>
        T Add(long diff);

        /// <summary>
        /// This as long minus <paramref name="subtrahend"/> as long.
        /// </summary>
        /// <param name="subtrahend">A value that is subtracted from this value.</param>
        long Diff(T subtrahend);
    }
}
