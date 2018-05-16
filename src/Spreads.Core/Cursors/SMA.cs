// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Cursors.Internal;
using Spreads.Cursors.Online;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public struct SMA<TKey, TValue, TCursor> :
        ICursorSeries<TKey, TValue, SMA<TKey, TValue, TCursor>>
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
        internal SpanOpImpl<TKey, TValue,
            TValue,
            SpanOp<TKey, TValue, TValue, TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>>,
            TCursor> _cursor;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal SpanOpImpl<TKey, TValue,
            TValue,
            SpanOp<TKey, TValue, TValue, TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>>,
            TCursor> _lookUpCursor;

        public CursorState State { get; internal set; }

        #endregion Cursor state

        #region Constructors

        internal SMA(TCursor cursor, int width = 1, bool allowIncomplete = false) : this()
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));

            var op = new SumAvgOnlineOp<TKey, TValue, TCursor>();
            var spanOp =
                new SpanOp<TKey, TValue, TValue,
                    TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>>(width, allowIncomplete, op, cursor.Comparer);
            _cursor =
                new SpanOpImpl<TKey, TValue,
                    TValue,
                    SpanOp<TKey, TValue, TValue, TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>
                    >,
                    TCursor
                >(cursor, spanOp);
        }

        internal SMA(TCursor cursor, TKey width, Lookup lookup) : this()
        {
            var op = new SumAvgOnlineOp<TKey, TValue, TCursor>();
            var spanOp =
                new SpanOp<TKey, TValue, TValue,
                    TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>>(width, lookup, op, cursor.Comparer);
            _cursor =
                new SpanOpImpl<TKey, TValue,
                    TValue,
                    SpanOp<TKey, TValue, TValue, TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>
                    >,
                    TCursor
                >(cursor, spanOp);
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public SMA<TKey, TValue, TCursor> Clone() // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            var instance = new SMA<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                State = State
            };
            return instance;
        }

        /// <inheritdoc />
        public SMA<TKey, TValue, TCursor> Initialize() // This causes SO when deeply nested [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            var instance = new SMA<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                State = CursorState.Initialized
            };
            return instance;
        }

        /// <inheritdoc />
        public void Dispose() // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for FillCursor
            _cursor.Dispose();
            if (!_lookUpCursor.Equals(default(SpanOpImpl<TKey, TValue,
                TValue,
                SpanOp<TKey, TValue, TValue, TCursor, SumAvgOnlineOp<TKey, TValue, TCursor>>,
                TCursor>)))
            {
                _lookUpCursor.Dispose();
            }
            State = CursorState.None;
        }

        /// <inheritdoc />
        public void Reset() // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            _cursor.Reset();
            State = CursorState.Initialized;
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
            // TODO why inlining breaks?
            // NB this causes SO in the TypeSystemSurvivesViolentAbuse test when run as Ctrl+F5 [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // ReSharper disable once ArrangeAccessorOwnerBody
            get { return new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue); }
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
        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentValue; }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch => null;

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Window cursor is discrete even if its input cursor is continuous.
        /// </summary>
        public bool IsContinuous => false;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_lookUpCursor.Equals(default(TCursor)))
            {
                _lookUpCursor = _cursor.Clone();
            }

            if (_lookUpCursor.MoveAt(key, Lookup.EQ))
            {
                value = _lookUpCursor.CurrentValue;
                return true;
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
            var moved = _cursor.MoveLast();
            if (moved)
            {
                State = CursorState.Moving;
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
            // TODO (review) why not just MoveNext? Inner must check it's state
            if (State < CursorState.Moving) return MoveFirst();
            return _cursor.MoveNext();
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
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        IReadOnlySeries<TKey, TValue> ICursor<TKey, TValue>.Source => new Series<TKey, TValue, SMA<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TValue, SMA<TKey, TValue, TCursor>> Source => new Series<TKey, TValue, SMA<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextAsync()
        {
            return MoveNextAsync(default);
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

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }
    }
}