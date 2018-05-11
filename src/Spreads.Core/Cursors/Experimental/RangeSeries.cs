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
using Spreads.Utils;

namespace Spreads.Cursors.Experimental
{
    /// <summary>
    /// A series that applies a selector to each value of its input series. Specialized for input cursor.
    /// </summary>
    [Obsolete("Use CursorSeries")] // TODO delete this class
    internal class RangeSeries<TKey, TValue, TCursor> :
        AbstractCursorSeries<TKey, TValue, RangeSeries<TKey, TValue, TCursor>>,
        ISpecializedCursor<TKey, TValue, RangeSeries<TKey, TValue, TCursor>> //, ICanMapValues<TKey, TValue>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        //nternal readonly ISeries<TKey, TValue> _series;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        private bool _endInclusive;
        private Opt<TKey> _endKey;
        private bool _startInclusive;
        private Opt<TKey> _startKey;
        private bool _atEnd;
        private bool _atStart;

        private Lookup _endLookup => _endInclusive ? Lookup.LE : Lookup.LT;
        private Lookup _startLookup => _startInclusive ? Lookup.GE : Lookup.GT;

        public RangeSeries()
        {
        }

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal RangeSeries(TCursor cursor,
            Opt<TKey> startKey, Opt<TKey> endKey,
            bool startInclusive = true, bool endInclusive = true)
        {
            if (cursor.Source.IsIndexed)
            {
                throw new NotSupportedException("RangeSeries is not supported for indexed series, only for sorted ones.");
            }

            _cursor = cursor;

            _startKey = startKey;
            _endKey = endKey;
            _startInclusive = startInclusive;
            _endInclusive = endInclusive;
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
                    return _cursor.Source.Updated;
                }
                return TaskUtil.FalseTask;
            }
        }

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public override bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public override bool IsCompleted => _cursor.Source.IsCompleted;

        // TODO when MNB works after MN
        /// <inheritdoc />
        public RangeSeries<TKey, TValue, TCursor> Clone()
        {
            var instance = GetUninitializedInstance();
            if (ReferenceEquals(instance, this))
            {
                return this;
            }

            instance._cursor = _cursor.Clone();
            instance._startKey = _startKey;
            instance._endKey = _endKey;
            instance._startInclusive = _startInclusive;
            instance._endInclusive = _endInclusive;
            instance.State = State;
            return instance;
        }

        /// <inheritdoc />
        public override RangeSeries<TKey, TValue, TCursor> Initialize()
        {
            var instance = GetUninitializedInstance();

            instance._cursor = _cursor.Initialize();
            instance._startKey = _startKey;
            instance._endKey = _endKey;
            instance._startInclusive = _startInclusive;
            instance._endInclusive = _endInclusive;

            instance.State = CursorState.Initialized;
            return instance;

        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
            ReleaseInstance(this);
        }

        //public Series<TKey, TResult> Map<TResult>(Func<TValue, TResult> selector, Func<Buffer<TValue>, Memory<TResult>> batchSelector)
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
                && _cursor.MoveAt(_startKey.Present, _startLookup)
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
                && _cursor.MoveAt(_endKey.Present, _endLookup)
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
        public Task<bool> MoveNextSpan(CancellationToken cancellationToken)
        {
            Trace.TraceWarning("MoveNextSpan is not implemented in RangeCursor");
            return TaskUtil.FalseTask;
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
            var c = _cursor.Comparer.Compare(key, _endKey.Present);
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
            var c = _cursor.Comparer.Compare(key, _startKey.Present);
            return _startInclusive ? c >= 0 : c > 0;
        }
    }
}