// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public struct ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>
        : ISpecializedCursor<TKey, TValue, ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>>
        where TCursorLeft : ISpecializedCursor<TKey, TValue, TCursorLeft>
        where TCursorRight : ISpecializedCursor<TKey, TValue, TCursorRight>
        where TOp : struct, IOp<TValue>
    {
        private Op2<TKey, TValue, TOp, Zip<TKey, TValue, TValue, TCursorLeft, TCursorRight>> _op;

        public ZipOp(Zip<TKey, TValue, TValue, TCursorLeft, TCursorRight> zip)
        {
            var op = new Op2<TKey, TValue, TOp, Zip<TKey, TValue, TValue, TCursorLeft, TCursorRight>>(zip);
            _op = op;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _op.MoveNextAsync();
        }

        public bool MoveNext()
        {
            return _op.MoveNext();
        }

        public void Reset()
        {
            _op.Reset();
        }

        public KeyValuePair<TKey, TValue> Current => _op.Current;

        object IEnumerator.Current => ((IEnumerator)_op).Current;

        public void Dispose()
        {
            _op.Dispose();
        }

        public Task DisposeAsync()
        {
            return _op.DisposeAsync();
        }

        public CursorState State => _op.State;

        public KeyComparer<TKey> Comparer => _op.Comparer;

        public bool MoveAt(TKey key, Lookup direction)
        {
            return _op.MoveAt(key, direction);
        }

        public bool MoveFirst()
        {
            return _op.MoveFirst();
        }

        public bool MoveLast()
        {
            return _op.MoveLast();
        }

        public long MoveNext(long stride, bool allowPartial)
        {
            return _op.MoveNext(stride, allowPartial);
        }

        public bool MovePrevious()
        {
            return _op.MovePrevious();
        }

        public long MovePrevious(long stride, bool allowPartial)
        {
            return _op.MovePrevious(stride, allowPartial);
        }

        public TKey CurrentKey => _op.CurrentKey;

        public TValue CurrentValue => _op.CurrentValue;

#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source => Source;
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation

        public Series<TKey, TValue, ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>> Source => new Series<TKey, TValue, ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>>(this);

        public bool IsContinuous => _op.IsContinuous;

        public ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight> Initialize()
        {
            return new ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>(_op._cursor.Initialize());
        }

        ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight> ISpecializedCursor<TKey, TValue, ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>>.Clone()
        {
            return new ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>(_op._cursor.Clone());
        }

        public bool IsIndexed => _op._cursor.IsIndexed;

        public bool IsCompleted => _op._cursor.IsCompleted;

        public IAsyncCompleter AsyncCompleter => _op._cursor.AsyncCompleter;

        public ICursor<TKey, TValue> Clone()
        {
            return _op.Clone();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _op.TryGetValue(key, out value);
        }
    }

    /// <summary>
    /// An <see cref="ISpecializedSeries{TKey,TValue,TCursor}"/> that applies an arithmetic operation
    /// <see cref="IOp{TValue}"/> to each value of its input series.
    /// </summary>
    public struct Op2<TKey, TValue, TOp, TCursor> :
        ISpecializedCursor<TKey, TValue, Op2<TKey, TValue, TOp, TCursor>>
        where TCursor : ISpecializedCursor<TKey, (TValue, TValue), TCursor>
        where TOp : struct, IOp<TValue>
    {
        private static TOp _op = default(TOp);

        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        //internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Op2(TCursor cursor) : this()
        {
            _cursor = cursor;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Op2<TKey, TValue, TOp, TCursor> Clone()
        {
            var instance = new Op2<TKey, TValue, TOp, TCursor>
            {
                _cursor = _cursor.Clone(),
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Op2<TKey, TValue, TOp, TCursor> Initialize()
        {
            var instance = new Op2<TKey, TValue, TOp, TCursor>
            {
                _cursor = _cursor.Initialize(),
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this ArithmeticCursor
            _cursor.Dispose();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotFiniteNumberException();
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
                return _op.Apply(_cursor.CurrentValue);
            }
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

        /// <inheritdoc />
        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.IsContinuous; }
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
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.IsCompleted; }
        }

        public IAsyncCompleter AsyncCompleter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.AsyncCompleter; }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = default(TOp).Apply(v);
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation

        /// <inheritdoc />
        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source => Source;

#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TValue, Op2<TKey, TValue, TOp, TCursor>> Source => new Series<TKey, TValue, Op2<TKey, TValue, TOp, TCursor>>(this);

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotImplementedException();
        }

        #endregion ICursor members

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }
    }
}