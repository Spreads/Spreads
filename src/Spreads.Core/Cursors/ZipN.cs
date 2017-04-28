// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors
{

    internal sealed class UnionKeys<TKey, TValue, TCursor> :
        CursorSeries<TKey, TValue, UnionKeys<TKey, TValue, TCursor>>,
        ICursor<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
    {
        private readonly ISeries<TKey, TValue>[] _series;
        private KeyComparer<TKey> _comparer;

        public UnionKeys(params ISeries<TKey, TValue>[] series)
        {
            _series = series;
            for (int i = 1; i < series.Length; i++)
            {
                if (!ReferenceEquals(series[i - 1].Comparer, series[i].Comparer))
                {
                    throw new ArgumentException("UnionKeys: Comparers are not equal");
                }
            }

            _comparer = series[0].Comparer;
        }

        public override KeyComparer<TKey> Comparer => _comparer;

        public override bool IsIndexed => false;

        // TODO this is heaviliy used in MNA, remove LINQ, use livecount, review
        public override bool IsReadOnly => _series.All(s => s.IsReadOnly);

        public override Task<bool> Updated => throw new NotImplementedException();

        public TKey CurrentKey => throw new NotImplementedException();

        public TValue CurrentValue => throw new NotImplementedException();

        public IReadOnlySeries<TKey, TValue> CurrentBatch => null;

        public bool IsContinuous => false;

        public KeyValuePair<TKey, TValue> Current => throw new NotImplementedException();

        object IEnumerator.Current => throw new NotImplementedException();

        public override UnionKeys<TKey, TValue, TCursor> Clone()
        {
            throw new NotImplementedException();
        }

        public override UnionKeys<TKey, TValue, TCursor> Initialize()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

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

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            throw new NotImplementedException();
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ZipN<TKey, TValue, TCursor> :
        CursorSeries<TKey, TValue[], ZipN<TKey, TValue, TCursor>>,
        ICursor<TKey, TValue[]>
        where TCursor : ICursor<TKey, TValue>
    {
        public override KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public override bool IsIndexed => throw new NotImplementedException();

        public override bool IsReadOnly => throw new NotImplementedException();

        public override Task<bool> Updated => throw new NotImplementedException();

        public TKey CurrentKey => throw new NotImplementedException();

        public TValue[] CurrentValue => throw new NotImplementedException();

        public IReadOnlySeries<TKey, TValue[]> CurrentBatch => throw new NotImplementedException();

        public bool IsContinuous => throw new NotImplementedException();

        public KeyValuePair<TKey, TValue[]> Current => throw new NotImplementedException();

        object IEnumerator.Current => throw new NotImplementedException();

        public override ZipN<TKey, TValue, TCursor> Clone()
        {
            throw new NotImplementedException();
        }

        public override ZipN<TKey, TValue, TCursor> Initialize()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

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

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue[] value)
        {
            throw new NotImplementedException();
        }

        ICursor<TKey, TValue[]> ICursor<TKey, TValue[]>.Clone()
        {
            throw new NotImplementedException();
        }
    }
}
