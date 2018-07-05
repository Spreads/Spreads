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
    /// <summary>
    /// An <see cref="ISpecializedCursor{TKey,TValue,TCursor}"/> that applies an operation to each value of its input series.
    /// </summary>
    public struct Comparison<TKey, TValue, TCursor> :
        ISpecializedCursor<TKey, bool, Comparison<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        private IOp<TValue, bool> _op;

        internal TValue _value;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        //internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Comparison(TCursor cursor, TValue value, IOp<TValue, bool> op) : this()
        {
            _op = op;
            _value = value;
            _cursor = cursor;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Comparison<TKey, TValue, TCursor> Clone()
        {
            var instance = new Comparison<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                _op = _op,
                _value = _value,
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Comparison<TKey, TValue, TCursor> Initialize()
        {
            var instance = new Comparison<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                _op = _op,
                _value = _value,
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this ComparisonCursor
            _cursor.Dispose();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _cursor.Reset();
        }

        ICursor<TKey, bool> ICursor<TKey, bool>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, bool> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, bool>(CurrentKey, CurrentValue); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            return _cursor.MovePrevious(stride, allowPartial);
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public bool CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _op.Apply(_cursor.CurrentValue, _value);
            }
        }

        /// <inheritdoc />
        public ISeries<TKey, bool> CurrentBatch
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
            get { return _cursor.Source.IsIndexed; }
        }

        /// <inheritdoc />
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.IsCompleted; }
        }

        public IAsyncCompleter AsyncCompleter => _cursor.AsyncCompleter;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out bool value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = _op.Apply(v, _value);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            return _cursor.MoveNext(stride, allowPartial);
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
            // TODO (?)
            return Utils.TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        ISeries<TKey, bool> ICursor<TKey, bool>.Source => Source;

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, bool, Comparison<TKey, TValue, TCursor>> Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new Series<TKey, bool, Comparison<TKey, TValue, TCursor>>(this); }
        }

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region Custom Properties

        /// <summary>
        /// A value used by TOp.
        /// </summary>
        public TValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _value; }
        }

        #endregion Custom Properties

       
        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }
    }
}