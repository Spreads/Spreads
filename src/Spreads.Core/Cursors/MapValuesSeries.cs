// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads.Cursors
{
    //internal interface ICanMapValues<TKey, TValue>
    //{
    //    BaseSeries<TKey, TResult> Map<TResult>(Func<TValue, TResult> selector, Func<Buffer<TValue>, Buffer<TResult>> batchSelector);
    //}

    /// <summary>
    /// A series that applies a selector to each value of its input series. Specialized for input cursor.
    /// </summary>
    public class MapValuesSeries<TKey, TValue, TResult, TCursor> :
        CursorSeries<TKey, TResult, MapValuesSeries<TKey, TValue, TResult, TCursor>>,
        ICursor<TKey, TResult> //, ICanMapValues<TKey, TResult>
        where TCursor : ICursor<TKey, TValue>
    {
        internal readonly Func<TValue, TResult> _selector;
        internal readonly ISeries<TKey, TValue> _series;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal MapValuesSeries(ISeries<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            _series = series;
            _cursor = GetCursor<TKey, TValue, TCursor>(_series);
            _selector = selector;
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TResult> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TResult>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TResult> CurrentBatch
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
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public TResult CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _selector(_cursor.CurrentValue); }
        }

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public override bool IsIndexed => _series.IsIndexed;

        /// <inheritdoc />
        public override bool IsReadOnly => _series.IsReadOnly;

        /// <inheritdoc />
        public override Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public override MapValuesSeries<TKey, TValue, TResult, TCursor> Clone()
        {
            var clone = Initialize();
            Debug.Assert(clone.State == CursorState.Initialized);
            if (State == CursorState.Moving)
            {
                clone.MoveAt(CurrentKey, Lookup.EQ);
            }
            return clone;
        }

        /// <inheritdoc />
        public override MapValuesSeries<TKey, TValue, TResult, TCursor> Initialize()
        {
            //if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            //{
            //    State = CursorState.Initialized;
            //    return this;
            //}
            var clone = new MapValuesSeries<TKey, TValue, TResult, TCursor>(_series, _selector);
            clone.State = CursorState.Initialized;
            return clone;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public override TResult GetAt(int idx)
        {
            return _selector(_cursor.Source.GetAt(idx));
        }

        public new MapValuesSeries<TKey, TValue, TResult, TCursor> GetEnumerator()
        {
            var clone = Initialize();
            return clone;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TResult value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = _selector(v);
                return true;
            }
            value = default(TResult);
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            var moved = _cursor.MoveAt(key, direction);
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            var moved = _cursor.MoveFirst();
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            var moved = _cursor.MoveLast();
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if ((int)State < (int)CursorState.Moving) return MoveFirst();
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            var moved = await _cursor.MoveNextBatch(cancellationToken);
            if (moved)
            {
                State = CursorState.BatchMoving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if ((int)State < (int)CursorState.Moving) return MoveLast();
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, TResult> ICursor<TKey, TResult>.Clone()
        {
            return Clone();
        }

        //BaseSeries<TKey, TResult1> ICanMapValues<TKey, TResult>.Map<TResult1>(Func<TResult, TResult1> selector, Func<Buffer<TResult>, Buffer<TResult1>> batchSelector)
        //{
        //    return new MapValuesSeries<TKey, TValue, TResult1, TCursor>(_series, CoreUtils.CombineMaps(_selector, selector));
        //}
    }
}