// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Cursors
{
    /// <summary>
    /// A generic operation on two values of types T1 and T2 returning a value of a type TResult.
    /// </summary>
    public interface IOp<T1, T2, TResult>
    {
        /// <summary>
        /// Apply a method to the first and the second argument.
        /// </summary>
        TResult Apply(T1 first, T2 second);
    }

    /// <summary>
    /// A generic operation on two values of a type T returning a value of a type TResult.
    /// </summary>
    public interface IOp<T, TResult> : IOp<T, T, TResult>
    {
    }

    /// <summary>
    /// A generic operation on two values of a type T returning a value of a type T.
    /// </summary>
    public interface IOp<T> : IOp<T, T>
    {
    }
}