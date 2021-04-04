// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Serialization
{

    // Need a common type that could wrap Array/Vec/VectorStorage with minimal overhead

    internal readonly struct ArrayWrapper<T>
    {
        public readonly T[] Array;
        public readonly bool Shuffle;
        public readonly bool Delta;

        public struct ArrayHeader
        {
            // Standard byte length prefix of any variable length type.
            private uint _byteLength;

            // 1 << 31 - shuffle flag
            // 1 << 30 - delta flag
            // 30 bits for count.
            private uint _elementCount;
        }

        public ArrayWrapper(T[] array, bool shuffle = false, bool delta = false)
        {
            Array = array;
            Shuffle = shuffle;
            Delta = delta;
        }
    }
}
