// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Spreads.Collections.Internal
{
    internal class DataBlockSource<TKey> : ISeries<TKey, DataBlock>
    {
        // TODO
        /// <summary>
        ///  For append-only containers blocks will have the same size. Last block could be only partially filled.
        /// </summary>
        public uint ConstantBlockLength = 0;

        public IAsyncEnumerator<KeyValuePair<TKey, DataBlock>> GetAsyncEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, DataBlock>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsCompleted => throw new NotImplementedException();

        public bool IsIndexed => throw new NotImplementedException();

        public ICursor<TKey, DataBlock> GetCursor()
        {
            throw new NotImplementedException();
        }

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, DataBlock>> First => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, DataBlock>> Last => throw new NotImplementedException();

        public DataBlock this[TKey key] => throw new NotImplementedException();

        public bool TryGetValue(TKey key, out DataBlock value)
        {
            throw new NotImplementedException();
        }

        public bool TryGetAt(long index, out KeyValuePair<TKey, DataBlock> kvp)
        {
            throw new NotImplementedException();
        }

        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, DataBlock> kvp)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TKey> Keys => throw new NotImplementedException();

        public IEnumerable<DataBlock> Values => throw new NotImplementedException();
    }
}