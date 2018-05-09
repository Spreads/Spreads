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
    /// <summary>
    /// An <see cref="ICursorSeries{TKey,TValue,TCursor}"/> that applies an operation to each value of its input series.
    /// </summary>
    public struct Comparison<TKey, TValue, TCursor> :
        ICursorSeries<TKey, bool, Comparison<TKey, TValue, TCursor>>
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
        public IReadOnlySeries<TKey, bool> CurrentBatch => throw new NotSupportedException();

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out bool value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = _op.Apply(v, _value);
                return true;
            }
            value = default(bool);
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
        IReadOnlySeries<TKey, bool> ICursor<TKey, bool>.Source => new Series<TKey, bool, Comparison<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, bool, Comparison<TKey, TValue, TCursor>> Source => new Series<TKey, bool, Comparison<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region Custom Properties

        /// <summary>
        /// A value used by TOp.
        /// </summary>
        public TValue Value => _value;

        #endregion Custom Properties

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