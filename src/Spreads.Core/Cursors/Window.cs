// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Cursors.Internal;
using Spreads.Cursors.Online;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public struct Window<TKey, TValue, TCursor> :
        ISpecializedCursor<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>>
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
            Range<TKey, TValue, TCursor>,
            SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>, TCursor, WindowOnlineOp<TKey, TValue, TCursor>>,
            TCursor> _cursor;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal SpanOpImpl<TKey, TValue,
            Range<TKey, TValue, TCursor>,
            SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>, TCursor, WindowOnlineOp<TKey, TValue, TCursor>>,
            TCursor> _lookUpCursor;

        #endregion Cursor state

        #region Constructors

        internal Window(TCursor cursor, int count, bool allowIncomplete = false) : this()
        {
            if (cursor.IsIndexed)
            {
                throw new NotSupportedException("Window is not supported for indexed series, only for sorted ones.");
            }
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

            var op = new WindowOnlineOp<TKey, TValue, TCursor>();
            var spanOp =
                new SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>,
                    TCursor, WindowOnlineOp<TKey, TValue, TCursor>>(count, allowIncomplete, op, cursor.Comparer);
            _cursor =
                new SpanOpImpl<TKey, TValue,
                    Range<TKey, TValue, TCursor>,
                    SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>, TCursor, WindowOnlineOp<TKey, TValue, TCursor>
                    >,
                    TCursor
                >(cursor, spanOp);
        }

        internal Window(TCursor cursor, TKey width, Lookup lookup) : this()
        {
            if (cursor.IsIndexed)
            {
                throw new NotSupportedException("Window is not supported for indexed series, only for sorted ones.");
            }
            var op = new WindowOnlineOp<TKey, TValue, TCursor>();
            var spanOp =
                new SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>,
                    TCursor, WindowOnlineOp<TKey, TValue, TCursor>>(width, lookup, op, cursor.Comparer);
            _cursor =
                new SpanOpImpl<TKey, TValue,
                    Range<TKey, TValue, TCursor>,
                    SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>, TCursor, WindowOnlineOp<TKey, TValue, TCursor>
                    >,
                    TCursor
                >(cursor, spanOp);
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public Window<TKey, TValue, TCursor> Clone() // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            var instance = new Window<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
            };
            return instance;
        }

        /// <inheritdoc />
        public Window<TKey, TValue, TCursor> Initialize() // This causes SO when deeply nested [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            var instance = new Window<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
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
                Range<TKey, TValue, TCursor>,
                SpanOp<TKey, TValue, Range<TKey, TValue, TCursor>, TCursor, WindowOnlineOp<TKey, TValue, TCursor>>,
                TCursor>)))
            {
                _lookUpCursor.Dispose();
            }
        }

        /// <inheritdoc />
        public void Reset() // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        {
            _cursor.Reset();
        }

        ICursor<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>> ICursor<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>> Current
        {
            // NB this causes SO in the TypeSystemSurvivesViolentAbuse test when run as Ctrl+F5 [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // ReSharper disable once ArrangeAccessorOwnerBody
            get { return new KeyValuePair<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>>(CurrentKey, CurrentValue); }
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
        public Series<TKey, TValue, Range<TKey, TValue, TCursor>> CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentValue.Source; }
        }

        /// <inheritdoc />
        public ISeries<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>> CurrentBatch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return null; }
        }

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.State; }
        }

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Comparer; }
        }

        object IEnumerator.Current => Current;

        /// <summary>
        /// Window cursor is discrete even if its input cursor is continuous.
        /// </summary>
        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return false; }
        }

        /// <inheritdoc />
        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.IsIndexed; }
        }

        /// <inheritdoc />
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.IsCompleted; }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out Series<TKey, TValue, Range<TKey, TValue, TCursor>> value)
        {
            if (_lookUpCursor.Equals(default(TCursor)))
            {
                _lookUpCursor = _cursor.Clone();
            }

            if (_lookUpCursor.MoveAt(key, Lookup.EQ))
            {
                value = _lookUpCursor.CurrentValue.Source;
                return true;
            }

            value = default;
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
        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch()
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
        ISeries<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>> ICursor<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>>.Source
        {
            get
            {
                return new Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>,
                    Window<TKey, TValue, TCursor>>(
                    this);
            }
        }

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Source
        {
            get
            {
                return new Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>,
                    Window<TKey, TValue, TCursor>>(
                    this);
            }
        }

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotSupportedException();
        }

        public IAsyncCompleter AsyncCompleter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.AsyncCompleter; }
        }

        #endregion ICursor members

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }
    }
}