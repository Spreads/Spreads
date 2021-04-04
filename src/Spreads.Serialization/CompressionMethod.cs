// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Serialization
{
    public enum CompressionMethod : byte
    {
        None = 0, // 00
        GZip = 1, // 01
        Lz4 = 2, // 10
        Zstd = 3, // 11

        // These are for completeness, but should not be used inside Spreads serialization
        Deflate = 200,
        ZLib = 201
    }
}
