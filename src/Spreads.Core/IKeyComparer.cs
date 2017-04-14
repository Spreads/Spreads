// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;

namespace Spreads
{
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
}