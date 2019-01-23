// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections;
using System.Collections.Generic;

namespace Spreads.Collections
{
    public interface IVec<out T> : IReadOnlyList<T>
    {
        /// <summary>
        /// Get the total number of elements in Vec.
        /// </summary>
        int Length
        {
            get;
        }

        /// <summary>
        /// Returns the element at the specified index.
        /// </summary>
        T Get(int index);

        /// <summary>
        /// Returns the element at the specified index without bound checks.
        /// </summary>
        T DangerousGet(int index);
    }

    public interface IVec
    {
        /// <summary>
        /// Get the total number of elements in Vec.
        /// </summary>
        int Length
        {
            get;
        }

        /// <summary>
        /// Returns the element at the specified index.
        /// </summary>
        T Get<T>(int index);

        /// <summary>
        /// Returns the element at the specified index without bound checks.
        /// </summary>
        T DangerousGet<T>(int index);
    }
}