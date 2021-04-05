// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.DataTypes
{
    public readonly struct FixedArray<T>
    {
        // Note that Count is defined at run-time.
        // Idea is that BinarySerializer could write header to a fixed destination.
        // If the destination is empty a header is written, if not empty then a
        // header must be equal. So if we are filling a block with values one-by-one
        // the first value sets the FixedArray size and then any attempt to write
        // a different value will fail.

        public readonly T[] Array; // TODO Memory<T>
        public readonly byte Count;

        public FixedArray(T[] array, byte count)
        {
            Array = array;
            Count = count;
        }
    }


}
