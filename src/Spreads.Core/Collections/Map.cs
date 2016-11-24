// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spreads.DataTypes;

namespace Spreads.Collections {

    // Series are panels with a single column
    // Panels are Series of rows

    // Untyped ordered map based on Variant types

    public class Map {
        private int _count;
        private Variant _keys;
        private Variant _values;
        private bool _isSorted;
    }


    public class TypedMap<TKey, TValue> : Map {

    }

    public class Panel<TRowKey, TColumnKey, TValue> {

    }

    // Columns are properties of TRow object
    // This is effectively series, the only di
    public class RowPanel<TRowKey, TRow> : Panel<TRowKey, string, Variant> {

    }


}
