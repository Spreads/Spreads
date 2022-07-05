// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;

namespace Spreads
{
    /// <summary>
    /// <see cref="IComparer{T}"/> with optional <see cref="IsDiffable"/>, <see cref="Diff"/> and <see cref="Add"/> members.
    /// </summary>
    public interface IKeyComparer<T> : IComparer<T>
    {
        /// <summary>
        /// True if a type T supports <see cref="Diff"/> and <see cref="Add"/> methods.
        /// </summary>
        bool IsDiffable { get; }

        /// <summary>
        /// If Diff(A,B) = X, then Add(A,X) = B, this is an opposite method for Diff.
        /// </summary>
        T Add(T value, long diff);

        /// <summary>
        /// Returns int64 distance between two values.
        /// </summary>
        /// <remarks>
        /// This method could be used for <see cref="IComparer{T}.Compare"/> implementation,
        /// but must be checked for int overflow (e.g. compare Diff result to 0L instead of int cast).
        /// </remarks>
        long Diff(T minuend, T subtrahend);
    }
}
