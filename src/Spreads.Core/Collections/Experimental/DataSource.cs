using System;

namespace Spreads.Collections.Experimental
{
    internal class DataSource
    {

    }

    internal class DataSource<T> : DataSource
    {

    }


    internal struct Cursor<T>
    {
        private T _current;

        public bool Move<TX>(long stride, bool allowPartial, ref TX target)
        {
            throw new NotImplementedException();
        }

        public bool Move(long stride, bool allowPartial)
        {
            return Move(stride, allowPartial, ref _current);
        }
    }
}
