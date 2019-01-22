// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spreads.Collections;
using Spreads.Collections.Internal;

namespace Spreads
{

    // For thread-safety save order version and just throw exn if that is changed on access
    // this should be rare and that branch should be predicted well
    // And b.t.w it's hard to test misprediction so in all our benhces it mostly predisted

    // TODO this should be a SortedMap itself? Or we should generate data storage for a chunk
    // it could be a case when getting a chunk is slower than gains from SIMD-math on them
    // so we probably need to slice or return existing storage

    // should probably do Series'3 implementation on those?

    // TODO? , ISpecializedCursor<Offset, TValue, SeriesSegment<Offset, TValue>>

    public struct Segment<TKey, TValue> : ISpecializedCursor<TKey, TValue, Segment<TKey, TValue>>
    {
        // don't forget about negative space - it could be used for order version or flags
        internal int _rowNumber;
        internal int _colNumber;
        internal DataStorage _storage;

        public Vector<TKey> Keys => throw new NotImplementedException();
        public Vector<TValue> Values => throw new NotImplementedException();
        public ValueTask<bool> MoveNextAsync()
        {
            var x = this.Source;
            throw new NotImplementedException();
        }

        public CursorState State => throw new NotImplementedException();

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public bool MoveAt(TKey key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public bool MoveFirst()
        {
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        bool ICursor<TKey, TValue>.MoveNext()
        {
            throw new NotImplementedException();
        }

        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public TKey CurrentKey => throw new NotImplementedException();

        public TValue CurrentValue => throw new NotImplementedException();

        public Series<TKey, TValue, Segment<TKey, TValue>> Source => new Series<TKey, TValue, Segment<TKey, TValue>>(this);

        public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source => throw new NotImplementedException();

        public bool IsContinuous => throw new NotImplementedException();

        public Segment<TKey, TValue> Initialize()
        {
            throw new NotImplementedException();
        }

        Segment<TKey, TValue> ISpecializedCursor<TKey, TValue, Segment<TKey, TValue>>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsIndexed => throw new NotImplementedException();

        public bool IsCompleted => throw new NotImplementedException();

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            throw new NotImplementedException();
        }

        bool IEnumerator.MoveNext()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public KeyValuePair<TKey, TValue> Current => throw new NotImplementedException();

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}