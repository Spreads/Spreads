// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.DataTypes.Experimental
{
    // TODO methods for rescale
    // TODO Alpha2/Alpha3 types similar to Symbol, always restricting to ASCII with the same capitalization (Upper)

    //[Obsolete("Probably useless data structure, UoMs usually common for all elelemts in a series")]
    // NB use case: multi-currency sales, where currency is specific to each transaction

    // TODO Zip on Panels: multyCncySales.Zip(rp(currenciesPanel), (tnx, rates) => tnx.Value * rates(tnx.Cncy))
    // Actually existing API allows to do so, but requires evaluation of the panel
    // Need a special method that will accept lambda to get column name, and lamdba with column and tnx
    // But existing API is very intuitive and will be quite fast already

    /// <summary>
    /// Fixed-point with runtime units of measure
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 16)]
    public struct Money
    {
        [FieldOffset(0)]
        private readonly UnitOfMeasure _nominator;

        [FieldOffset(4)]
        private readonly UnitOfMeasure _denominator;

        [FieldOffset(8)]
        private readonly Price _value;        // byte
    }
}