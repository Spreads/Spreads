using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Cursors.Internal
{
    // Value is lagged cursor.

    internal struct LagStepImpl<TKey, TValue, TCursor> :
        ICursor<TKey, TCursor, LagStepImpl<TKey, TValue, TCursor>>
        where TCursor : ICursor<TKey, TValue, TCursor>
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
        internal TCursor _laggedCursor;

        internal int _width;
        internal int _step;
        internal bool _allowIncomplete;
        internal int _currentWidth;

        public CursorState State { get; internal set; }

        internal TCursor CurrentCursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor; }
        }

        internal TCursor LaggedCursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _laggedCursor; }
        }

        #endregion Cursor state

        #region Constructors

        internal LagStepImpl(TCursor cursor, int width = 1, int step = 1, bool allowIncomplete = false) : this()
        {
            if (width <= 0) { ThrowHelper.ThrowArgumentOutOfRangeException(nameof(width)); }
            if (step <= 0) { ThrowHelper.ThrowArgumentOutOfRangeException(nameof(step)); }
            if (allowIncomplete && _step > 1) { ThrowHelper.ThrowNotImplementedException("TODO incomplete with step is not implemented"); }

            _cursor = cursor;

            _width = width;
            _step = step;
            _allowIncomplete = allowIncomplete;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LagStepImpl<TKey, TValue, TCursor> Clone()
        {
            var instance = new LagStepImpl<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                _laggedCursor = _laggedCursor.Clone(),
                _width = _width,
                _step = _step,
                _allowIncomplete = _allowIncomplete,
                _currentWidth = _currentWidth,
                State = State
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LagStepImpl<TKey, TValue, TCursor> Initialize()
        {
            var instance = new LagStepImpl<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                _laggedCursor = _cursor.Initialize(),
                _width = _width,
                _step = _step,
                _allowIncomplete = _allowIncomplete,
                _currentWidth = 0,
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
            // constructor could be uninitialized but contain some state, e.g. _value for this FillCursor
            _cursor.Dispose();
            _laggedCursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _cursor.Reset();
            _laggedCursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, TCursor> ICursor<TKey, TCursor>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, TCursor> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TCursor>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public TCursor CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _laggedCursor; }
        }

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <summary>
        /// LagImpl cursor is discrete even if its input cursor is continuous.
        /// </summary>
        public bool IsContinuous => false;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TCursor value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }

            var wasMoving = State == CursorState.Moving;

            var ck = wasMoving ? _cursor.CurrentKey : default(TKey);
            var lk = wasMoving ? _laggedCursor.CurrentKey : default(TKey);

            var moved = _cursor.MoveAt(key, direction);

            if (moved)
            {
                if (wasMoving)
                {
                    _laggedCursor.Dispose();
                }
                _laggedCursor = _cursor.Clone();

                // step is irrelevant here, need to move lagged by width
                _currentWidth = 1;
                while (_currentWidth < _width)
                {
                    moved = _laggedCursor.MovePrevious();
                    _currentWidth++;
                }

                // NB important to do inside if, first move must be true
                moved = moved || _allowIncomplete;
            }

            if (moved)
            {
                State = CursorState.Moving;
            }
            else if (wasMoving && !_cursor.MoveAt(ck, Lookup.EQ) && _laggedCursor.MoveAt(lk, Lookup.EQ))
            {
                ThrowHelper.ThrowOutOfOrderKeyException(ck);
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

            var wasMoving = State == CursorState.Moving;

            var moved = _cursor.MoveFirst();

            if (moved)
            {
                if (wasMoving)
                {
                    _laggedCursor.Dispose();
                }
                _laggedCursor = _cursor.Clone();

                _currentWidth = 1;
                while (_currentWidth < _width)
                {
                    moved = _cursor.MoveNext();
                    _currentWidth++;
                }

                // NB important to do inside if, first move must be true
                moved = moved || _allowIncomplete;
            }

            if (moved)
            {
                State = CursorState.Moving;
            }
            else if (State == CursorState.Moving)
            {
                // if cursor was moving that it must have had at least one value
                ThrowHelper.ThrowOutOfOrderKeyException(_cursor.CurrentKey);
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
            var wasMoving = State == CursorState.Moving;

            var moved = _cursor.MoveFirst();

            if (moved)
            {
                if (wasMoving)
                {
                    _laggedCursor.Dispose();
                }
                _laggedCursor = _cursor.Clone();

                _currentWidth = 1;
                while (_currentWidth < _width)
                {
                    moved = _laggedCursor.MovePrevious();
                    _currentWidth++;
                }

                // NB important to do inside if, first move must be true
                moved = moved || _allowIncomplete;
            }

            if (moved)
            {
                State = CursorState.Moving;
            }
            else if (State == CursorState.Moving)
            {
                // if cursor was moving that it must have had at least one value
                ThrowHelper.ThrowOutOfOrderKeyException(_cursor.CurrentKey);
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            var ck = _cursor.CurrentKey;

            if (_allowIncomplete) ThrowHelper.ThrowNotImplementedException("TODO");

            var moved = false;
            var moves = 0;
            while (moves < _step)
            {
                moved = _cursor.MoveNext() && _laggedCursor.MoveNext(); // NB order, lagged moves only after current moves
                moves++;
            }

            if (!moved)
            {
                // roll back
                for (var i = 0; i < moves; i++)
                {
                    try
                    {
                        var movedBack = _cursor.MovePrevious() && _laggedCursor.MovePrevious();
                        if (!movedBack)
                        {
                            ThrowHelper.ThrowInvalidOperationException("MovePrevious should succeed after MoveNextAsync or throw OutOfOrderKeyException");
                        }
                    }
                    catch (OutOfOrderKeyException<TKey>)
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(ck);
                    }
                }
            }

            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();

            var ck = _cursor.CurrentKey;

            if (_allowIncomplete) ThrowHelper.ThrowNotImplementedException("TODO");

            var moved = false;
            var moves = 0;
            while (moves < _step)
            {
                moved = _laggedCursor.MovePrevious() && _cursor.MovePrevious(); // NB order, current moves only after lagged moves
                moves++;
            }

            if (!moved)
            {
                // roll back
                for (var i = 0; i < moves; i++)
                {
                    try
                    {
                        var moveForward = _laggedCursor.MoveNext() && _cursor.MoveNext();
                        if (!moveForward)
                        {
                            ThrowHelper.ThrowInvalidOperationException("MoveNextAsync should succeed after MovePrevious or throw OutOfOrderKeyException");
                        }
                    }
                    catch (OutOfOrderKeyException<TKey>)
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(ck);
                    }
                }
            }

            return moved;
        }

        /// <inheritdoc />
        ISeries<TKey, TCursor> ICursor<TKey, TCursor>.Source => new Series<TKey, TCursor, LagStepImpl<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TCursor, LagStepImpl<TKey, TValue, TCursor>> Source => new Series<TKey, TCursor, LagStepImpl<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => _cursor.IsIndexed;

        /// <inheritdoc />
        public bool IsCompleted
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.IsCompleted; }
        }

        public IAsyncCompleter AsyncCompleter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.AsyncCompleter; }
        }

        #endregion ICursorSeries members

        public ValueTask DisposeAsync()
        {
            Dispose();
            return new ValueTask(Task.CompletedTask);
        }
    }
}