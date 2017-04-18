// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Blittable DateTime
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    [Serialization(BlittableSize = 8)]
    public unsafe struct Timestamp
    {
        private readonly long _value;

        public static implicit operator DateTime(Timestamp timestamp)
        {
            return *(DateTime*)(void*)&timestamp;
        }
        public static implicit operator Timestamp(DateTime dateTime)
        {
            return *(Timestamp*)(void*)&dateTime;
        }

        // TODO IConvertible and other standard interfaces of DateTime that fallback to DT implementation via conversion
    }
}