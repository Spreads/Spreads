// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A known type to represent an error code as a wrapper over Int64.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
    [Serialization(BlittableSize = 8)]
    public struct ErrorCode
    {
        public long Code;
    }
}