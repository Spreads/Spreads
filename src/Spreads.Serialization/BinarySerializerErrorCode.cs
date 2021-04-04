// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Serialization
{
    public enum BinarySerializerErrorCode : int
    {
        /// <summary>
        /// Destination buffer is smaller than serialized payload size.
        /// </summary>
        NotEnoughCapacity = -1,

        /// <summary>
        /// Value header does not match expected header.
        /// </summary>
        HeaderMismatch = -2,
    }
}
