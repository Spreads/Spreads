using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors.Internal
{
    internal struct SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor> :
        ICursorSeries<TKey, TResult, SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TSpanOp : struct, ISpanOp<TKey, TValue, TResult, TCursor> //, ICursorOnlineOp2<TKey, TValue, TResult, TCursor>
    {
        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _cursor;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _laggedCursor;

        // this field keeps the state of the online op and must be cloned
        private TSpanOp _op;

        internal CursorState State { get; set; }

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

        internal TSpanOp OnlineOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _op; }
        }

        #endregion Cursor state

        #region Constructors

        internal SpanOpImpl(TCursor cursor, TSpanOp op) : this()
        {
            _cursor = cursor;
            _op = op;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor> Clone()
        {
            // TODO (perf) this could be expensive, but smarter implementation would require TSpanOp to have Clone() method
            var instance = Initialize();
            instance.MoveAt(CurrentKey, Lookup.EQ);
            return instance;

            //var instance = new OnlineOpImpl<TKey, TValue, TResult, TOnlineOp, TCursor>
            //{
            //    _cursor = _cursor.Clone(),
            //    _laggedCursor = _laggedCursor.Clone(),
            //    _op = _op,
            //    State = State
            //};
            //return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor> Initialize()
        {
            var newOp = _op;
            newOp.Reset();
            var instance = new SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor>
            {
                _cursor = _cursor.Initialize(),
                _laggedCursor = _cursor.Initialize(),
                _op = newOp,
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
            _laggedCursor.Dispose();
            _op.Reset();
            State = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _cursor.Reset();
            _laggedCursor.Reset();
            _op.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, TResult> ICursor<TKey, TResult>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, TResult> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TResult>(CurrentKey, CurrentValue); }
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
            get { return _op.GetResult(ref _laggedCursor, ref _cursor); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TResult> CurrentBatch => null;

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <summary>
        /// LagImpl cursor is discrete even if its input cursor is continuous.
        /// </summary>
        public bool IsContinuous => false;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TResult value)
        {
            throw new NotSupportedException("OnlineOpImpl is always discrete, to get value at any point one should use a copy as a navigation cursor and call MoveAt");
        }

        /// <summary>
        /// Helper method to reduce args typing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Expand()
        {
            return _op.Expand(ref _laggedCursor, ref _cursor);
        }

        /// <summary>
        /// If expand is zero after just one move, the cursor is probably in the situation
        /// when incomplete spans are allowed. It must be eager and try to move left cursor back as much as possible.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryExpandEager()
        {
            var expand = 0;
            var lmoved = true;
            while (expand == 0 && lmoved)
            {
                if (_op.IsForwardOnly)
                {
                    ThrowHelper.ThrowNotImplementedException();
                }
                lmoved = _laggedCursor.MovePrevious();
                if (lmoved)
                {
                    _op.AddNewLeft(ref _laggedCursor);
                    expand = Expand();
                    if (expand < 0)
                    {
                        lmoved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                        expand = Expand();
                        if (expand == 0)
                        {
                            break;
                        }
                        ThrowHelper.ThrowInvalidOperationException();
                    }
                    else if (expand > 0)
                    {
                        lmoved = _laggedCursor.MovePrevious();
                        if (lmoved)
                        {
                            _op.AddNewLeft(ref _laggedCursor);
                            expand = Expand();
                            if (expand < 0)
                            {
                                lmoved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                                expand = Expand();
                                if (expand == 0)
                                {
                                    break;
                                }
                                ThrowHelper.ThrowInvalidOperationException();
                            }
                        }
                        else
                        {
                            // NB expand could switch to zero if it was non-zero speculatively to retry
                            // so we need to re-evaluate it here
                            expand = Expand();
                            if (expand == 0)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            if (expand != 0) ThrowHelper.ThrowInvalidOperationException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }

            _op.Reset();

            var wasMoving = State == CursorState.Moving;

            var ck = wasMoving ? _cursor.CurrentKey : default(TKey);

            var moved = _cursor.MoveAt(key, direction);

            if (moved)
            {
                if (wasMoving)
                {
                    _laggedCursor.Dispose();
                }
                _laggedCursor = _cursor.Clone();

                _op.AddNewRight(ref _cursor);

                var expand = Expand();

                if (expand < 0)
                {
                    ThrowHelper.ThrowInvalidOperationException("Initial OnNext should not return a negative value");
                }

                if (expand == 0)
                {
                    TryExpandEager();
                }

                while (expand != 0 && moved)
                {
                    if (_op.IsForwardOnly || expand < 0)
                    {
                        ThrowHelper.ThrowNotImplementedException();
                    }
                    else
                    {
                        moved = _laggedCursor.MovePrevious();
                        if (moved)
                        {
                            _op.AddNewLeft(ref _laggedCursor);
                            expand = Expand();
                            if (expand < 0) // Span is too big
                            {
                                moved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                                expand = Expand();
                            }
                        }
                        else if (direction == Lookup.GE || direction == Lookup.GT)
                        {
                            moved = _cursor.MoveNext();
                            if (moved)
                            {
                                _op.AddNewRight(ref _cursor);
                                expand = Expand();
                                if (expand < 0) // Span is too big
                                {
                                    moved = _op.RemoveAndMovePreviousRight(ref _cursor);
                                    if (Settings.AdditionalCorrectnessChecks.DoChecks && !moved)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException("Lagged cursor should always move next toward current cursor");
                                    }
                                    expand = Expand();
                                    if (expand == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (moved)
            {
                State = CursorState.Moving;
            }
            else if (wasMoving)
            {
                if (!MoveAt(ck, Lookup.EQ))
                {
                    ThrowHelper.ThrowOutOfOrderKeyException(ck);
                }
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

            _op.Reset();

            var wasMoving = State == CursorState.Moving;

            var moved = _cursor.MoveFirst();

            if (moved)
            {
                if (wasMoving)
                {
                    _laggedCursor.Dispose();
                }
                // at its first position where _cursor is
                _laggedCursor = _cursor.Clone();

                // applying only the current value, lagged is equal to it
                _op.AddNewRight(ref _cursor);

                var expand = Expand();

                if (expand < 0)
                {
                    ThrowHelper.ThrowInvalidOperationException("Initial OnNext should not return a negative value");
                }

                while (expand != 0 && moved)
                {
                    moved = _cursor.MoveNext();
                    if (moved)
                    {
                        _op.AddNewRight(ref _cursor);
                        expand = Expand();
                        if (expand < 0)
                        {
                            moved = _op.RemoveAndMovePreviousRight(ref _cursor);
                            expand = Expand();
                        }
                    }
                }
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

            _op.Reset();

            var wasMoving = State == CursorState.Moving;

            var moved = _cursor.MoveLast();

            if (moved)
            {
                if (wasMoving)
                {
                    _laggedCursor.Dispose();
                }
                _laggedCursor = _cursor.Clone();

                _op.AddNewRight(ref _cursor);

                var expand = Expand();

                if (expand < 0)
                {
                    ThrowHelper.ThrowInvalidOperationException("Initial OnNext should not return a negative value");
                }

                // if expand is zero after just one move,
                // we are probably at the situation when incomplete spans are allowed
                // must be eager and try to move left cursor back as much as possible
                if (expand == 0)
                {
                    TryExpandEager();
                }

                while (expand != 0 && moved)
                {
                    if (_op.IsForwardOnly)
                    {
                        ThrowHelper.ThrowNotImplementedException();
                    }
                    else
                    {
                        moved = _laggedCursor.MovePrevious();
                        if (moved)
                        {
                            _op.AddNewLeft(ref _laggedCursor);
                            expand = Expand();
                            if (expand < 0)
                            {
                                moved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                                expand = Expand();
                            }
                        }
                    }
                }
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
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            var ck = _cursor.CurrentKey;

            var moved = _cursor.MoveNext();

            if (!moved)
            {
                // false MoveNextAsync doesn't change the state of _cursor, could just return false
                // _cursor is an anchor for keys
                return false;
            }

            _op.AddNewRight(ref _cursor);

            var expand = Expand();

            // here expand == 0 is not suspicious because it is not the first state update
            // _op must return non-zero if the state now is valid but there could be a better state
            // e.g. for minimum width moving R to the right after a valid state is definitely a
            // valid state, so the _op.Expand() must return zero only when changing from invalid
            // position to a valid one.

            while (expand != 0 && moved)
            {
                if (expand < 0)
                {
                    moved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                    expand = Expand();
                    if (Settings.AdditionalCorrectnessChecks.DoChecks)
                    {
                        if (!moved)
                        {
                            ThrowHelper.ThrowInvalidOperationException("Lagged cursor should always move next toward current cursor");
                        }
                        //if (_cursor.Comparer.Compare(_laggedCursor.CurrentKey, _cursor.CurrentKey) > 0)
                        //{
                        //    ThrowHelper.ThrowInvalidOperationException("Lagged cursor moved ahead of the current one.");
                        //}
                    }
                }
                else if (expand > 0)
                {
                    moved = _laggedCursor.MovePrevious();
                    if (moved)
                    {
                        _op.AddNewLeft(ref _laggedCursor);
                        expand = Expand();
                    }
                    else
                    {
                        // NB expand could switch to zero if it was non-zero speculatively to retry
                        // so we need to re-evaluate it here
                        expand = Expand();
                        if (expand == 0)
                        {
                            moved = true;
                        }
                    }
                }
            }

            // TODO test the case when !moved
            if (!moved && !MoveAt(ck, Lookup.EQ))
            {
                ThrowHelper.ThrowOutOfOrderKeyException(ck);
            }
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
            return TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();

            //ThrowHelper.ThrowNotImplementedException();
            //return false;

            var ck = _cursor.CurrentKey;

            var moved = _op.RemoveAndMovePreviousRight(ref _cursor);
            if (!moved)
            {
                return false;
            }

            var expand = Expand();

            // if expand is zero after shrinking,
            // we are probably at the situation when incomplete spans are allowed
            // must be eager and try to move left cursor back as much as possible
            var lmoved = true;
            while (expand == 0 && lmoved)
            {
                if (_op.IsForwardOnly)
                {
                    ThrowHelper.ThrowNotImplementedException();
                }
                lmoved = _laggedCursor.MovePrevious();
                if (lmoved)
                {
                    _op.AddNewLeft(ref _laggedCursor);
                    expand = Expand();
                    if (expand < 0)
                    {
                        lmoved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                        expand = Expand();
                        if (expand == 0)
                        {
                            break;
                        }
                        ThrowHelper.ThrowInvalidOperationException();
                    }
                    else if (expand > 0)
                    {
                        ThrowHelper.ThrowNotImplementedException();
                    }
                }
            }

            while (expand != 0 && moved)
            {
                if (expand < 0)
                {
                    moved = _op.RemoveAndMoveNextLeft(ref _laggedCursor);
                    expand = Expand();
                    if (Settings.AdditionalCorrectnessChecks.DoChecks)
                    {
                        if (!moved)
                        {
                            ThrowHelper.ThrowInvalidOperationException("Lagged cursor should always move next toward current cursor");
                        }
                        if (_cursor.Comparer.Compare(_laggedCursor.CurrentKey, _cursor.CurrentKey) > 0)
                        {
                            ThrowHelper.ThrowInvalidOperationException("Lagged cursor moved ahead of the current one.");
                        }
                    }
                }
                else if (expand > 0)
                {
                    moved = _laggedCursor.MovePrevious();
                    if (moved)
                    {
                        _op.AddNewLeft(ref _laggedCursor);
                        expand = Expand();
                    }
                    else
                    {
                        // NB expand could switch to zero if it was non-zero speculatively to retry
                        // so we need to re-evaluate it here
                        expand = Expand();
                        if (expand == 0)
                        {
                            moved = true;
                        }
                    }
                }
            }

            // TODO test the case when !moved
            if (!moved && !MoveAt(ck, Lookup.EQ))
            {
                ThrowHelper.ThrowOutOfOrderKeyException(ck);
            }
            return moved;
        }

        /// <inheritdoc />
        IReadOnlySeries<TKey, TResult> ICursor<TKey, TResult>.Source => new Series<TKey, TResult, SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TResult, SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor>> Source => new Series<TKey, TResult, SpanOpImpl<TKey, TValue, TResult, TSpanOp, TCursor>>(this);

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