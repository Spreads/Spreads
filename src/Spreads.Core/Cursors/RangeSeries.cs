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
    /// <summary>
    /// A series that applies a selector to each value of its input series. Specialized for input cursor.
    /// </summary>
    public class RangeSeries<TKey, TValue, TCursor> :
        CursorSeries<TKey, TValue, RangeSeries<TKey, TValue, TCursor>>,
        ISpecializedCursor<TKey, TValue, RangeSeries<TKey, TValue, TCursor>> //, ICanMapValues<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
    {
        internal readonly ISeries<TKey, TValue> _series;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        private readonly bool _endInclusive;
        private readonly Opt<TKey> _endKey;
        private readonly bool _startInclusive;
        private readonly Opt<TKey> _startKey;
        private bool _atEnd;
        private bool _atStart;
        private Lookup _endLookup;
        private Lookup _startLookup;

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal RangeSeries(ISeries<TKey, TValue> series,
            Opt<TKey> startKey, Opt<TKey> endKey,
            bool startInclusive = true, bool endInclusive = true)
        {
            if (series.IsIndexed)
            {
                throw new NotSupportedException(
                    "RangeSeries is not supported for indexed series, only for sorted ones.");
            }

            _series = series;

            _startKey = startKey;
            _endKey = endKey;
            _startInclusive = startInclusive;
            _endInclusive = endInclusive;

            _startLookup = startInclusive ? Lookup.GE : Lookup.GT;
            _endLookup = endInclusive ? Lookup.LE : Lookup.LT;
        }

        /// <summary>
        /// End key is inclusive or missing.
        /// </summary>
        public bool EndInclusive => _endInclusive;

        /// <summary>
        /// Start key is inclusive or missing.
        /// </summary>
        public bool StartInclusive => _startInclusive;

        /// <summary>
        /// End key
        /// </summary>
        public Opt<TKey> EndKey => _endKey;

        /// <summary>
        /// Start key
        /// </summary>
        public Opt<TKey> StartKey => _startKey;

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch => throw new NotSupportedException();

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentValue; }
        }

        /// <inheritdoc />
        public override Task<bool> Updated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_atEnd && (State != CursorState.Moving | EndOk(_cursor.CurrentKey)))
                {
                    return _series.Updated;
                }
                return TaskEx.FalseTask;
            }
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

        // TODO when MNB works after MN
        /// <inheritdoc />
        public override RangeSeries<TKey, TValue, TCursor> Clone()
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
        public override RangeSeries<TKey, TValue, TCursor> Initialize()
        {
            if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            {
                _cursor = GetCursor<TKey, TValue, TCursor>(_series);
                State = CursorState.Initialized;
                return this;
            }
            var clone = new RangeSeries<TKey, TValue, TCursor>(_series, _startKey, _endKey, _startInclusive, _endInclusive);
            // NB recursive call but it should always hit the if case above
            var initialized = clone.Initialize();
            Debug.Assert(ReferenceEquals(clone, initialized));
            return initialized;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        //public BaseSeries<TKey, TResult> Map<TResult>(Func<TValue, TResult> selector, Func<Buffer<TValue>, Buffer<TResult>> batchSelector)
        //{
        //    return new MapValuesSeries<TKey, TValue, TResult, RangeSeries<TKey, TValue, TCursor>>(_series, selector);
        //}

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            // must return to the position if false move
            var beforeMove = _cursor.CurrentKey;
            var moved = _cursor.MoveAt(key, direction);
            if (InRange(_cursor.CurrentKey))
            {
                Debug.Assert(State > 0);
                // keep navigating state unchanged
                if (moved && State == CursorState.Initialized)
                {
                    State = CursorState.Moving;
                }
                _atEnd = false;
                _atStart = false;
                return moved;
            }
            if (moved)
            {
                _cursor.MoveAt(beforeMove, Lookup.EQ);
            }
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            if ((_startKey.IsPresent
                && _cursor.MoveAt(_startKey.Value, _startLookup)
                && InRange(_cursor.CurrentKey))
                || (!_startKey.IsPresent && _cursor.MoveFirst()))
            {
                Debug.Assert(State > 0);
                if (State == CursorState.Initialized)
                {
                    State = CursorState.Moving;
                }
                _atEnd = false;
                _atStart = false;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            if ((_endKey.IsPresent
                && _cursor.MoveAt(_endKey.Value, _endLookup)
                && InRange(_cursor.CurrentKey))
                || (!_endKey.IsPresent && _cursor.MoveLast()))
            {
                Debug.Assert(State > 0);
                if (State == CursorState.Initialized)
                {
                    State = CursorState.Moving;
                }
                _atEnd = false;
                _atStart = false;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if ((int)State < (int)CursorState.Moving) return MoveFirst();

            if (!_endKey.IsPresent)
            {
                return _cursor.MoveNext();
            }
            if (_atEnd)
            {
                return false;
            }

            var beforeMove = _cursor.CurrentKey;
            var moved = _cursor.MoveNext();
            if (EndOk(_cursor.CurrentKey))
            {
                return moved;
            }
            if (!moved) return false;
            _cursor.MoveAt(beforeMove, Lookup.EQ);
            _atEnd = true;

            return false;
        }

        /// <inheritdoc />

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            Trace.TraceWarning("MoveNextBatch is not implemented in RangeCursor");
            return TaskEx.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if ((int)State < (int)CursorState.Moving) return MoveLast();

            if (!_startKey.IsPresent)
            {
                return _cursor.MovePrevious();
            }
            if (_atStart)
            {
                return false;
            }

            var beforeMove = _cursor.CurrentKey;
            var moved = _cursor.MovePrevious();
            if (StartOk(_cursor.CurrentKey))
            {
                return moved;
            }

            if (!moved) return false;
            _cursor.MoveAt(beforeMove, Lookup.EQ);
            _atStart = true;

            return false;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (InRange(key))
            {
                return _cursor.TryGetValue(key, out value);
            }
            value = default(TValue);
            return false;
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EndOk(TKey key)
        {
            if (!_endKey.IsPresent) return true;
            var c = _cursor.Comparer.Compare(key, _endKey.Value);
            return _endInclusive ? c <= 0 : c < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InRange(TKey key)
        {
            return StartOk(key) && EndOk(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StartOk(TKey key)
        {
            if (!_startKey.IsPresent) return true;
            var c = _cursor.Comparer.Compare(key, _startKey.Value);
            return _startInclusive ? c >= 0 : c > 0;
        }
    }
}