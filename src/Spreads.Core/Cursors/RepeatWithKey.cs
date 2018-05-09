// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    // TODO Optimization in TGV (i.e. current lack of it) shows up in SCM backed by storage to such
    // extent that I have to do all work in memory using ToSortedMap(). But SMs are quite fast for TGV,
    // require a binary search. But still it is Log(n) vs best case Log(1)

    /// <summary>
    /// A continuous cursor <see cref="ICursorSeries{TKey,TValue,TCursor}"/> that returns a key and a value
    /// at or before (<see cref="Lookup.LE"/>) a requested key in <see cref="TryGetValue"/>.
    /// It delegates moves directly to the underlying cursor.
    /// </summary>
    public struct RepeatWithKey<TKey, TValue, TCursor> :
        ICursorSeries<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _lookUpCursor;

        internal KeyComparer<TKey> _cmp;

        internal (bool wasMovingNext, (TKey key, TValue value) previous) _previousState;
        internal bool _lookUpIsMoving;

        internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal RepeatWithKey(TCursor cursor) : this()
        {
            _cursor = cursor;
            _cmp = cursor.Comparer;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RepeatWithKey<TKey, TValue, TCursor> Clone()
        {
            var instance = new RepeatWithKey<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                _cmp = _cmp,
                _previousState = _previousState,
                _lookUpCursor = _lookUpCursor.Clone(),
                State = State
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RepeatWithKey<TKey, TValue, TCursor> Initialize()
        {
            var instance = new RepeatWithKey<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                _cmp = _cmp,
                _previousState = default((bool wasMovingNext, (TKey, TValue) previous)),
                _lookUpCursor = _cursor.Clone(),
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
            // constructor could be uninitialized but contain some state, e.g. _value for FillCursor
            _cursor.Dispose();
            _previousState = default((bool wasMovingNext, (TKey, TValue) previous));
            _lookUpCursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _previousState = default((bool wasMovingNext, (TKey, TValue) previous));
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, (TKey, TValue)> ICursor<TKey, (TKey, TValue)>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, (TKey, TValue)> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, (TKey, TValue)>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public (TKey, TValue) CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_cursor.CurrentKey, _cursor.CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, (TKey, TValue)> CurrentBatch => null;

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cmp;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => true;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out (TKey, TValue) value)
        {
            // TODO two optimizations
            // 1. (Done) after all MN create a state with valid previous KVP, and check if TGV tries
            //    to get a value in between the previous and the current position. This
            //    optimization targets how Zip works.
            // 2. (Done, but commented out, not profitable) move lookup cursor one time and during the move keep previous position.
            //    then the same logic as in 1.
            // Could use both or conditionally depending on relative positoin to key.

            if (_previousState.wasMovingNext
                && _cmp.Compare(key, _previousState.previous.key) >= 0
                && _cmp.Compare(key, _cursor.CurrentKey) < 0)
            {
                value = (_previousState.previous.key, _previousState.previous.value);
                return true;
            }

            if (State == CursorState.Moving && _cmp.Compare(key, _cursor.CurrentKey) == 0)
            {
                value = (_cursor.CurrentKey, _cursor.CurrentValue);
                return true;
            }

            // TODO (low) review, now this is not profitable for ZipN, but doesn't hurt.
            //if (_lookUpIsMoving)
            //{
            //    //if (_cmp.Compare(key, _lookUpCursor.CurrentKey) == 0)
            //    //{
            //    //    value = (_lookUpCursor.CurrentKey, _lookUpCursor.CurrentValue);
            //    //    return true;
            //    //}
            //    (TKey key, TValue value) previous = (_lookUpCursor.CurrentKey, _lookUpCursor.CurrentValue);
            //    if (_lookUpCursor.MoveNextAsync()
            //        && _cmp.Compare(key, previous.key) >= 0
            //        && _cmp.Compare(key, _lookUpCursor.CurrentKey) < 0)
            //    {
            //        value = (previous.key, previous.value);
            //        return true;
            //    }
            //    if (_cmp.Compare(key, _lookUpCursor.CurrentKey) == 0)
            //    {
            //        value = (_lookUpCursor.CurrentKey, _lookUpCursor.CurrentValue);
            //        return true;
            //    }
            //}

            //if (_cursor.TryGetValue(key, out var v))
            //{
            //    value = (key, v);
            //    return true;
            //}

            if (_lookUpCursor.MoveAt(key, Lookup.LE))
            {
                _lookUpIsMoving = true;
                value = (_lookUpCursor.CurrentKey, _lookUpCursor.CurrentValue);
                return true;
            }
            value = default((TKey, TValue));
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
            _previousState.wasMovingNext = false;
            var moved = _cursor.MoveAt(key, direction);
            if (moved)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNextAsync
        public bool MoveFirst()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            _previousState.wasMovingNext = false;
            var moved = _cursor.MoveFirst();
            if (moved)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MovePrevious
        public bool MoveLast()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            _previousState.wasMovingNext = false;
            var moved = _cursor.MoveLast();
            if (moved)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            _previousState.previous.key = _cursor.CurrentKey;
            // TODO this is eager now
            _previousState.previous.value = _cursor.CurrentValue;
            var moved = _cursor.MoveNext();
            _previousState.wasMovingNext = moved;
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            return Utils.TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();
            _previousState.wasMovingNext = false;
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        IReadOnlySeries<TKey, (TKey, TValue)> ICursor<TKey, (TKey, TValue)>.Source => new Series<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, TCursor>> Source => new Series<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public bool IsReadOnly
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.IsCompleted; }
        }

        /// <inheritdoc />
        public Task<bool> Updated
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.Updated; }
        }

        #endregion ICursorSeries members
    }

    public struct Repeat<TKey, TValue, TCursor> :
        ICursorSeries<TKey, TValue, Repeat<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal RepeatWithKey<TKey, TValue, TCursor> _cursor;

        //internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Repeat(TCursor cursor) : this()
        {
            _cursor = new RepeatWithKey<TKey, TValue, TCursor>(cursor);
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public Repeat<TKey, TValue, TCursor> Clone()
        {
            var instance = new Repeat<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
            };
            return instance;
        }

        /// <inheritdoc />
        public Repeat<TKey, TValue, TCursor> Initialize()
        {
            var instance = new Repeat<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
            };
            return instance;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for FillCursor
            _cursor.Dispose();
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

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
            get { return _cursor.CurrentValue.Item2; }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch => null;

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => true;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = v.Item2;
                return true;
            }

            value = default(TValue);
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            var moved = _cursor.MoveAt(key, direction);

            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNextAsync
        public bool MoveFirst()
        {
            var moved = _cursor.MoveFirst();

            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MovePrevious
        public bool MoveLast()
        {
            var moved = _cursor.MoveLast();

            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            return Utils.TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        IReadOnlySeries<TKey, TValue> ICursor<TKey, TValue>.Source => new Series<TKey, TValue, Repeat<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TValue, Repeat<TKey, TValue, TCursor>> Source => new Series<TKey, TValue, Repeat<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public bool IsReadOnly
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.IsCompleted; }
        }

        /// <inheritdoc />
        public Task<bool> Updated
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.Updated; }
        }

        #endregion ICursorSeries members
    }
}