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
using Spreads.DataTypes;

// ReSharper disable once CheckNamespace
namespace Spreads
{

    // TODO review naming, Stat2Cursor -> Stat2, Stat2 -> M2

    public struct Stat2Cursor<TKey, TValue, TCursor> :
        ICursorSeries<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>>
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
            Stat2<TKey>,
            SpanOp<TKey, TValue, Stat2<TKey>, TCursor, Stat2OnlineOp<TKey, TValue, TCursor>>,
            TCursor> _cursor;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal SpanOpImpl<TKey, TValue,
            Stat2<TKey>,
            SpanOp<TKey, TValue, Stat2<TKey>, TCursor, Stat2OnlineOp<TKey, TValue, TCursor>>,
            TCursor> _lookUpCursor;

        internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Stat2Cursor(TCursor cursor, int width = 1, bool allowIncomplete = false) : this()
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));

            var op = new Stat2OnlineOp<TKey, TValue, TCursor>();
            var spanOp =
                new SpanOp<TKey, TValue, Stat2<TKey>,
                    TCursor, Stat2OnlineOp<TKey, TValue, TCursor>>(width, allowIncomplete, op);
            _cursor =
                new SpanOpImpl<TKey, TValue,
                    Stat2<TKey>,
                    SpanOp<TKey, TValue, Stat2<TKey>, TCursor, Stat2OnlineOp<TKey, TValue, TCursor>
                    >,
                    TCursor
                >(cursor, spanOp);
        }

        internal Stat2Cursor(TCursor cursor, TKey width, Lookup lookup) : this()
        {
            var op = new Stat2OnlineOp<TKey, TValue, TCursor>();
            var spanOp =
                new SpanOp<TKey, TValue, Stat2<TKey>,
                    TCursor, Stat2OnlineOp<TKey, TValue, TCursor>>(width, lookup, op);
            _cursor =
                new SpanOpImpl<TKey, TValue,
                    Stat2<TKey>,
                    SpanOp<TKey, TValue, Stat2<TKey>, TCursor, Stat2OnlineOp<TKey, TValue, TCursor>
                    >,
                    TCursor
                >(cursor, spanOp);
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stat2Cursor<TKey, TValue, TCursor> Clone()
        {
            var instance = new Stat2Cursor<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                State = State
            };
            return instance;
        }

        /// <inheritdoc />
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stat2Cursor<TKey, TValue, TCursor> Initialize() // This causes SO when deeply nested 
        {
            var instance = new Stat2Cursor<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                State = CursorState.Initialized
            };
            return instance;
        }

        /// <inheritdoc />
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for FillCursor
            _cursor.Dispose();
            if (!_lookUpCursor.Equals(default(SpanOpImpl<TKey, TValue,
                Stat2<TKey>,
                SpanOp<TKey, TValue, Stat2<TKey>, TCursor, Stat2OnlineOp<TKey, TValue, TCursor>>,
                TCursor>)))
            {
                _lookUpCursor.Dispose();
            }
            State = CursorState.None;
        }

        /// <inheritdoc />
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, Stat2<TKey>> ICursor<TKey, Stat2<TKey>>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, Stat2<TKey>> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, Stat2<TKey>>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public Stat2<TKey> CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentValue; }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, Stat2<TKey>> CurrentBatch => null;

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Window cursor is discrete even if its input cursor is continuous.
        /// </summary>
        public bool IsContinuous => false;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out Stat2<TKey> value)
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

            value = default(Stat2<TKey>);
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
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNext
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
        public bool MoveNext()
        {
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
        IReadOnlySeries<TKey, Stat2<TKey>> ICursor<TKey, Stat2<TKey>>.Source => new Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>> Source => new Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken)
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
            get { return _cursor.Source.IsReadOnly; }
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