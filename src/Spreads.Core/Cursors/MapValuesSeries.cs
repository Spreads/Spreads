// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads.Cursors
{
    internal interface ICanMapValues<TKey, TValue>
    {
        BaseSeries<TKey, TResult> Map<TResult>(Func<TValue, TResult> selector, Func<Buffer<TValue>, Buffer<TResult>> batchSelector);
    }

    /// <summary>
    /// A series that applies a selector to each value of its input series. Specialized for input cursor.
    /// </summary>
    public class MapValuesSeries<TKey, TValue, TResult, TCursor> : CursorSeries<TKey, TResult,
        MapValuesSeries<TKey, TValue, TResult, TCursor>>, ICanMapValues<TKey, TResult>
        where TCursor : ICursor<TKey, TValue>
    {
        internal readonly ISeries<TKey, TValue> _series;
        internal readonly Func<TValue, TResult> _selector;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal MapValuesSeries(ISeries<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            _series = series;

            // TODO (low) this pattern is repeating and not ideal
            // However, specialized impl is only used internally (no public constructor)

            var c = _series.GetCursor();
            if (c is BaseCursorAsync<TKey, TValue, TCursor> bca)
            {
                _cursor = bca._innerCursor;
            }
            else if (c is TCursor tCursor)
            {
                _cursor = tCursor;
            }
            else
            {
                var e = series.GetEnumerator();
                if (e is BaseCursorAsync<TKey, TValue, TCursor> bca1)
                {
                    _cursor = bca1._innerCursor;
                }
                if (e is TCursor tCursor1)
                {
                    _cursor = tCursor1;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            _selector = selector;
        }

        /// <inheritdoc />
        public override TKey CurrentKey => _cursor.CurrentKey;

        /// <inheritdoc />
        public override TResult CurrentValue => _selector(_cursor.CurrentValue);

        /// <inheritdoc />
        public override IReadOnlySeries<TKey, TResult> CurrentBatch
        {
            get
            {
                var batch = _cursor.CurrentBatch;
                // TODO when batching is proper implemented (nested batches) reuse an instance for this
                var mapped = new MapValuesSeries<TKey, TValue, TResult, TCursor>(batch, _selector);
                return mapped;
            }
        }

        /// <inheritdoc />
        public override bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

        public override Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public override bool IsIndexed => _series.IsIndexed;

        /// <inheritdoc />
        public override bool IsReadOnly => _series.IsReadOnly;

        /// <inheritdoc />
        public override MapValuesSeries<TKey, TValue, TResult, TCursor> Create()
        {
            if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            {
                State = CursorState.Initialized;
                return this;
            }
            var clone = new MapValuesSeries<TKey, TValue, TResult, TCursor>(_series, _selector);
            clone.State = CursorState.Initialized;
            return clone;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public override TResult GetAt(int idx)
        {
            return _selector(_cursor.Source.GetAt(idx));
        }

        /// <inheritdoc />
        public override bool MoveAt(TKey key, Lookup direction)
        {
            return _cursor.MoveAt(key, direction);
        }

        /// <inheritdoc />
        public override bool MoveFirst()
        {
            return _cursor.MoveFirst();
        }

        /// <inheritdoc />
        public override bool MoveLast()
        {
            return _cursor.MoveLast();
        }

        /// <inheritdoc />
        public override bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        public override Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            return _cursor.MoveNextBatch(cancellationToken);
        }

        /// <inheritdoc />
        public override bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        public override void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        BaseSeries<TKey, TResult1> ICanMapValues<TKey, TResult>.Map<TResult1>(Func<TResult, TResult1> selector, Func<Buffer<TResult>, Buffer<TResult1>> batchSelector)
        {
            return new MapValuesSeries<TKey, TValue, TResult1, TCursor>(_series, CoreUtils.CombineMaps(_selector, selector));
        }
    }

    /// <summary>
    /// A series that applies a selector to each value of its input series.
    /// </summary>
    public class MapValuesSeries<TKey, TValue, TResult> : MapValuesSeries<TKey, TValue, TResult, ICursor<TKey, TValue>>
    {
        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        public MapValuesSeries(ISeries<TKey, TValue> series, Func<TValue, TResult> selector) : base(series, selector)
        {
        }
    }
}