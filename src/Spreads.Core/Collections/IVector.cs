// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Collections
{
    public interface IVector<out T>
    {
        /// <summary>
        /// Get the total number of items in IVector.
        /// </summary>
        int Length
        {
            get;
        }

        // TODO rename to GetItem(index)
        /// <summary>
        /// Returns an item at the specified index.
        /// </summary>
        T GetItem(int index);

        /// <summary>
        /// Returns an item at the specified index without bound checks.
        /// </summary>
        T DangerousGetItem(int index);
    }

    public interface IVector
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
        /// Returns the element at the specified index without bound and type checks.
        /// </summary>
        T DangerousGet<T>(int index);
    }
}
