// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spreads.DataTypes;

namespace Spreads.Collections
{
    // TODO this is draft of draft...

    // base class to represent tabular data, inclusing series (single column)
    // BaseDataTable is Variant array of

    public class BaseDataTable
    {
        internal Variant Root;
        internal Variant RowIndex;
        internal Variant ColumnIndex;
        internal Variant Columns;

        public BaseDataTable()
        {
            var columns = new Variant();
            var rootArray = new Variant[3];
        }
    }
}