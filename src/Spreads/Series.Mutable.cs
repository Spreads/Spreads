// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spreads
{
    public partial class Series<TKey, TValue> : IMutableSeries<TKey, TValue>
    {
        [Obsolete]
        public long Count => throw new System.NotImplementedException();

        public long Version => throw new System.NotImplementedException();

        [Obsolete]
        public bool IsAppendOnly => throw new System.NotImplementedException();

        public Task<bool> Set(TKey key, TValue value)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> TryAdd(TKey key, TValue value)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> TryAddFirst(TKey key, TValue value)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<Opt<TValue>> TryRemove(TKey key)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst()
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast()
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction)
        {
            throw new System.NotImplementedException();
        }

        public ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
        {
            throw new System.NotImplementedException();
        }
    }
}
