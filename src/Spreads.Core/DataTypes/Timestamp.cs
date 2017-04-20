// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A Timestamp stored as nanoseconds since Unix epoch as UInt64.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    [Serialization(BlittableSize = 8)]
    public unsafe struct Timestamp
    {
        private readonly long _value;

        public static implicit operator DateTime(Timestamp timestamp)
        {
            throw new NotImplementedException();
        }

        public static implicit operator Timestamp(DateTime dateTime)
        {
            throw new NotImplementedException();
        }

        // TODO IConvertible and other standard interfaces of DateTime that fallback to DT implementation via conversion
    }
}