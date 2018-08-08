// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    
    /// <summary>
    /// Fixed-point high-precision value.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 16)]
    public struct Money
    {
        // Both integer and fraction are longs. This allows to store Ethereum wei (smallest known and widely used unit)
        // and 184467x of World GDP in USD or c.1.5x in IRR.

        private readonly long _integer;
        private readonly long _fraction;
    }
}