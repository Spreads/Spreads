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

        public RowCursor GetRowCursor(IEnumerable<Schema.Column> columnsNeeded, Random rand = null)
        {
            throw new NotImplementedException();
        }

        public RowCursor[] GetRowCursorSet(IEnumerable<Schema.Column> columnsNeeded, int n, Random rand = null)
        {
            throw new NotImplementedException();
        }

        public bool CanShuffle => throw new NotImplementedException();

        public Schema Schema => throw new NotImplementedException();
    }
}
