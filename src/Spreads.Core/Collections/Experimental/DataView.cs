using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.DataView;

namespace Spreads.Collections.Experimental
{
    public struct DV : IDataView
    {
        public long? GetRowCount()
        {
            throw new NotImplementedException();
        }

        public DataViewRowCursor GetRowCursor(IEnumerable<DataViewSchema.Column> columnsNeeded, Random rand = null)
        {
            throw new NotImplementedException();
        }

        public DataViewRowCursor[] GetRowCursorSet(IEnumerable<DataViewSchema.Column> columnsNeeded, int n, Random rand = null)
        {
            throw new NotImplementedException();
        }

        public bool CanShuffle => throw new NotImplementedException();

        public DataViewSchema Schema => throw new NotImplementedException();
    }
}
