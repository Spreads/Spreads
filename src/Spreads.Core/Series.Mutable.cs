// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;

namespace Spreads
{
    public class MutableSeries<TKey, TValue> : AppendSeries<TKey, TValue>, IMutableSeries<TKey, TValue>
    {
        public new TValue this[TKey key]
        {
            get => base[key];
            set => throw new NotImplementedException();
        }

        public bool Set(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        public void Set<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            throw new NotImplementedException();
        }

        public bool TryAdd(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        public void Add(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryAdd<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            throw new NotImplementedException();
        }

        public void Add<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            throw new NotImplementedException();
        }

        public bool TryPrepend(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        public void Prepend(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryPrepend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            throw new NotImplementedException();
        }

        public void Prepend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            throw new NotImplementedException();
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveFirst(out KeyValuePair<TKey, TValue> pair)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveLast(out KeyValuePair<TKey, TValue> pair)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveMany(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> pair)
        {
            throw new NotImplementedException();
        }

        public void MarkAppendOnly()
        {
            throw new NotImplementedException();
        }
    }
}
