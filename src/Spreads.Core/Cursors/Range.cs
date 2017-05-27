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
namespace Spreads
{
    /// <summary>
    /// Range cursor.
    /// </summary>
    public struct Range<TKey, TValue, TCursor> :
        ICursorSeries<TKey, TValue, Range<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        #region Cursor state

        private bool _endInclusive;
        private Opt<TKey> _endKey;
        private bool _startInclusive;
        private Opt<TKey> _startKey;
        private bool _atEnd;
        private bool _atStart;

        private Lookup _endLookup => _endInclusive ? Lookup.LE : Lookup.LT;
        private Lookup _startLookup => _startInclusive ? Lookup.GE : Lookup.GT;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Range(TCursor cursor,
            Opt<TKey> startKey, Opt<TKey> endKey,
            bool startInclusive = true, bool endInclusive = true) : this()
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

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public Range<TKey, TValue, TCursor> Clone()
        {
            var instance = new Range<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                _startKey = _startKey,
                _endKey = _endKey,
                _startInclusive = _startInclusive,
                _endInclusive = _endInclusive,
                State = State
            };

            return instance;
        }

        /// <inheritdoc />
        public Range<TKey, TValue, TCursor> Initialize()
        {
            var instance = new Range<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                _startKey = _startKey,
                _endKey = _endKey,
                _startInclusive = _startInclusive,
                _endInclusive = _endInclusive,
                State = CursorState.Initialized
            };

            return instance;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this RangeCursor
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region Custom members

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

        #endregion Custom members

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue); }
        }

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
            get
            {
                return _cursor.CurrentValue;
            }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch => throw new NotSupportedException();

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

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

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
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
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNext
        public bool MoveFirst()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            if ((_startKey.IsPresent
                 && _cursor.MoveAt(_startKey.Value, _startLookup)
                 && InRange(_cursor.CurrentKey))
                || (!_startKey.IsPresent && _cursor.MoveFirst()))
            {
                State = CursorState.Moving;

                _atEnd = false;
                _atStart = false;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MovePrevious
        public bool MoveLast()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            if ((_endKey.IsPresent
                && _cursor.MoveAt(_endKey.Value, _endLookup)
                && InRange(_cursor.CurrentKey))
                || (!_endKey.IsPresent && _cursor.MoveLast()))
            {
                State = CursorState.Moving;
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
            if (State < CursorState.Moving) return MoveFirst();

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
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            Trace.TraceWarning("MoveNextBatch is not implemented in RangeCursor");
            return TaskEx.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();

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

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TValue, Range<TKey, TValue, TCursor>> Source => new Series<TKey, TValue, Range<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        IReadOnlySeries<TKey, TValue> ICursor<TKey, TValue>.Source => Source;

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region ISpecializedCursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public bool IsReadOnly
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.IsReadOnly; }
        }

        /// <inheritdoc />
        public Task<bool> Updated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_atEnd && (State != CursorState.Moving | EndOk(_cursor.CurrentKey)))
                {
                    return _cursor.Source.Updated;
                }
                return TaskEx.FalseTask;
            }
        }

        #endregion ISpecializedCursorSeries members
    }
}