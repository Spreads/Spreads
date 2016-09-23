using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spreads.Collections
{

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
