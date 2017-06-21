// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// Range cursor.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Range<TKey, TValue, TCursor> :
        ICursorSeries<TKey, TValue, Range<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        [Flags]
        private enum Flags
        {
            EndInclusive = 1,
            EndKeyIsPresent = 2,
            StartInclusive = 4,
            StartKeyIsPresent = 8,
            AtEnd = 16,
            AtStart = 32
        }

        #region Cursor state

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        /// <remarks>
        /// Avoid callvirt to getter, which could potentially be not inlined like in SM case with F#
        /// </remarks>
        internal KeyComparer<TKey> _cmp;

        private TKey _endKey;
        private TKey _startKey;

        /// <remarks>
        /// Number of elements consumed by subsequent MoveNext moves.
        /// All other moves reset this value to zero.
        /// If _count > 0 and _steps == _count we are at the end, could skip MoveNext from this position
        /// and avoid recovery. If there was any other move than MN this check will just never work, no need
        /// to add complex logic - this is for foreach-like/LINQ forward-only consumption.
        /// </remarks>
        private int _steps;

        /// <remarks>
        /// Positive when range is set from SpanOp and is a Window
        /// </remarks>
        private int _count;

        /// <summary>
        /// True if the cursor was given in a moving state and positioned at the range's start.
        /// Helps to avoid _cursor.MoveAt in MoveFirst
        /// </summary>
        internal bool _isWindow;

        private Flags _flags;

        internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Range(TCursor cursor,
            Opt<TKey> startKey, Opt<TKey> endKey,
            bool startInclusive = true, bool endInclusive = true,
            bool isWindow = false,
            int count = -1) : this()
        {
            if (cursor.Source.IsIndexed)
            {
                throw new NotSupportedException("RangeSeries is not supported for indexed series, only for sorted ones.");
            }

            if (isWindow &&
                (startKey.IsMissing || endKey.IsMissing || !startInclusive || !endInclusive
                    || cursor.Comparer.Compare(cursor.CurrentKey, startKey.Present) != 0))
            {
                ThrowHelper.ThrowInvalidOperationException("Wrong constructor arguments for cursorIsClonedAtStart == true case");
            }

            _cursor = cursor;
            _cmp = cursor.Comparer;

            _isWindow = isWindow;

            if (startKey.IsPresent)
            {
                _flags |= Flags.StartKeyIsPresent;
                _startKey = startKey.Present;
            }

            if (endKey.IsPresent)
            {
                _flags |= Flags.EndKeyIsPresent;
                _endKey = endKey.Present;
            }

            if (startInclusive)
            {
                _flags |= Flags.StartInclusive;
            }

            if (endInclusive)
            {
                _flags |= Flags.EndInclusive;
            }

            _count = count;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Range<TKey, TValue, TCursor> Clone()
        {
            var instance = new Range<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                _cmp = _cmp,
                _isWindow = _isWindow,
                _startKey = _startKey,
                _endKey = _endKey,
                _flags = _flags,
                _count = _count,
                _steps = _steps,
                State = State
            };

            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Range<TKey, TValue, TCursor> Initialize()
        {
            var clearFlags = (_flags & ~Flags.AtStart) & ~Flags.AtEnd;
            var instance = new Range<TKey, TValue, TCursor>
            {
                _cursor = _isWindow ? _cursor.Clone() : _cursor.Initialize(),
                _cmp = _cmp,
                _isWindow = _isWindow,
                _startKey = _startKey,
                _endKey = _endKey,
                _flags = clearFlags,
                _count = _count,
                _steps = 0,
                State = CursorState.Initialized
            };

            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this RangeCursor
            _flags = (_flags & ~Flags.AtStart) & ~Flags.AtEnd;
            _cursor.Dispose();
            _steps = 0;
            State = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _flags = (_flags & ~Flags.AtStart) & ~Flags.AtEnd;
            _cursor.Reset();
            _steps = 0;
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
        public bool EndInclusive => (_flags & Flags.EndInclusive) == Flags.EndInclusive;

        /// <summary>
        /// Start key is inclusive or missing.
        /// </summary>
        public bool StartInclusive => (_flags & Flags.StartInclusive) == Flags.StartInclusive;

        /// <summary>
        /// End key
        /// </summary>
        public Opt<TKey> EndKey => (_flags & Flags.EndKeyIsPresent) == Flags.EndKeyIsPresent ? Opt.Present(_endKey) : Opt<TKey>.Missing;

        /// <summary>
        /// Start key
        /// </summary>
        public Opt<TKey> StartKey => (_flags & Flags.StartKeyIsPresent) == Flags.StartKeyIsPresent ? Opt.Present(_startKey) : Opt<TKey>.Missing;

        /// <summary>
        /// If positive then this range has known count.
        /// </summary>
        public long Count => _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EndOk(TKey key)
        {
            if (!_isWindow && (_flags & Flags.EndKeyIsPresent) != Flags.EndKeyIsPresent) return true;
            // If EndInclusive then c == 0 is OK. We could subtract the flag from c and alway use c < 0
            var c = _cmp.Compare(key, _endKey) - (int)(_flags & Flags.EndInclusive);
            return c < 0; //(_flags & Flags.EndInclusive) == Flags.EndInclusive ? c <= 0 : c < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InRange(TKey key)
        {
            return StartOk(key) && EndOk(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool StartOk(TKey key)
        {
            if ((_flags & Flags.StartKeyIsPresent) != Flags.StartKeyIsPresent) return true;
            var c = _cmp.Compare(key, _startKey);
            return (_flags & Flags.StartInclusive) == Flags.StartInclusive ? c >= 0 : c > 0;
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
        public KeyComparer<TKey> Comparer => _cmp;

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
            _steps = 0;
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
                _flags &= ~Flags.AtEnd;
                _flags &= ~Flags.AtStart;
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
            _steps = 1;
            if (State != CursorState.Moving && _isWindow)
            {
                Debug.Assert(_cmp.Compare(_cursor.CurrentKey, _startKey) == 0);
                State = CursorState.Moving;
                return true;
            }

            if (((_flags & Flags.StartKeyIsPresent) == Flags.StartKeyIsPresent
                 && _cursor.MoveAt(_startKey, (_flags & Flags.StartInclusive) == Flags.StartInclusive ? Lookup.GE : Lookup.GT)
                 && InRange(_cursor.CurrentKey))
                || ((_flags & Flags.StartKeyIsPresent) != Flags.StartKeyIsPresent && _cursor.MoveFirst()))
            {
                State = CursorState.Moving;

                _flags &= ~Flags.AtEnd;
                _flags &= ~Flags.AtStart;
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
            _steps = 0;
            if (((_flags & Flags.EndKeyIsPresent) == Flags.EndKeyIsPresent
                && _cursor.MoveAt(_endKey, (_flags & Flags.EndInclusive) == Flags.EndInclusive ? Lookup.LE : Lookup.LT)
                && InRange(_cursor.CurrentKey))
                || ((_flags & Flags.EndKeyIsPresent) != Flags.EndKeyIsPresent && _cursor.MoveLast()))
            {
                State = CursorState.Moving;
                _flags &= ~Flags.AtEnd;
                _flags &= ~Flags.AtStart;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            if (!_isWindow && (_flags & Flags.EndKeyIsPresent) != Flags.EndKeyIsPresent)
            {
                return _cursor.MoveNext();
            }
            if ((_flags & Flags.AtEnd) == Flags.AtEnd)
            {
                return false;
            }

            var beforeMove = _cursor.CurrentKey;

            var moved = _cursor.MoveNext();

            if (!moved) return false;

            // NB First check of EndOk(_cursor.CurrentKey) is done above. This is the remaining part.
            var endIsOk = _cmp.Compare(_cursor.CurrentKey, _endKey) - (_flags & Flags.EndInclusive) < 0;

            if (endIsOk)
            {
                if (_isWindow && ++_steps == _count)
                {
                    // next MN will return false without trying to move beyond the end and subsequent recovery with MP
                    _flags |= Flags.AtEnd;
                    Debug.Assert(_cmp.Compare(_cursor.CurrentKey, _endKey) == 0);
                    //if (Settings.AdditionalCorrectnessChecks.DoChecks)
                    //{
                    //    if (_cmp.Compare(_cursor.CurrentKey, _endKey) != 0)
                    //    {
                    //        ThrowHelper.ThrowInvalidOperationException("Window end key and count do not match");
                    //    }
                    //}
                }
                return true;
            }

            if (_cursor.MovePrevious())
            {
                _flags |= Flags.AtEnd;
            }
            else
            {
                ThrowHelper.ThrowOutOfOrderKeyException(beforeMove);
            }

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
            _steps = 0;
            Trace.TraceWarning("MoveNextBatch is not implemented in RangeCursor");
            return Utils.TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();
            _steps = 0;
            if ((_flags & Flags.StartKeyIsPresent) != Flags.StartKeyIsPresent)
            {
                return _cursor.MovePrevious();
            }
            if ((_flags & Flags.AtStart) == Flags.AtStart)
            {
                return false;
            }

            var beforeMove = _cursor.CurrentKey;

            var moved = _cursor.MovePrevious();

            if (!moved) return false;

            if (StartOk(_cursor.CurrentKey))
            {
                return true;
            }

            if (_cursor.MoveNext())
            {
                _flags |= Flags.AtStart;
            }
            else
            {
                ThrowHelper.ThrowOutOfOrderKeyException(beforeMove);
            }

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
                if ((_flags & Flags.AtEnd) != Flags.AtEnd && (State != CursorState.Moving | EndOk(_cursor.CurrentKey)))
                {
                    return _cursor.Source.Updated;
                }
                return Utils.TaskUtil.FalseTask;
            }
        }

        #endregion ISpecializedCursorSeries members
    }
}