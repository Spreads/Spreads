// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors
{
    /// <summary>
    /// An <see cref="ICursorSeries{TKey,TValue,TCursor}"/> that applies an arithmetic operation to each value of its input series.
    /// </summary>
    public struct ArithmeticCursor<TKey, TValue, TOp, TCursor> :
        ICursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, TOp, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TOp : struct, IOp<TValue>
    {
        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        internal TValue _value;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal ArithmeticCursor(TCursor cursor, TValue value) : this()
        {
            _value = value;
            _cursor = cursor;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public ArithmeticCursor<TKey, TValue, TOp, TCursor> Clone()
        {
            var instance = new ArithmeticCursor<TKey, TValue, TOp, TCursor>();
            instance._cursor = _cursor.Clone();
            instance._value = _value;
            instance.State = State;
            return instance;
        }

        /// <inheritdoc />
        public ArithmeticCursor<TKey, TValue, TOp, TCursor> Initialize()
        {
            var instance = new ArithmeticCursor<TKey, TValue, TOp, TCursor>();
            instance._cursor = _cursor.Initialize();
            instance._value = _value;
            instance.State = CursorState.Initialized;
            return instance;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this ArithmeticCursor
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public void Reset()
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
                return default(TOp).Apply(_cursor.CurrentValue, _value);
            }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch => throw new NotImplementedException();

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = default(TOp).Apply(v, _value);
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
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        IReadOnlySeries<TKey, TValue> ICursor<TKey, TValue>.Source => new CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="CursorSeries{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, TOp, TCursor>> Source => new CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(this);

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region Custom Properties

        /// <summary>
        /// A value used by TOp.
        /// </summary>
        public TValue Value => _value;

        #endregion


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

        // TODO delete this
        ///// <inheritdoc />
        //public TValue GetAt(int idx)
        //{
        //    return default(TOp).Apply(_cursor.Source.GetAt(idx), _value);
        //}

        #endregion ISpecializedCursorSeries members

        //BaseSeries<TKey, TValue1> ICanMapValues<TKey, TValue>.Map<TValue1>(Func<TValue, TValue1> selector, Func<Buffer<TValue>, Buffer<TValue1>> batchSelector)
        //{
        //    if (batchSelector != null)
        //    {
        //        throw new NotImplementedException();
        //    }
        //    return new MapValuesSeries<TKey, TValue, TValue1, TCursor>(_series, CoreUtils.CombineMaps<TValue, TValue, TValue1>(Apply, selector));
        //}

        #region Unary Operators

        // NB This allows to combine arithmetic operators using sealed ArithmeticCursor<> as TCursor
        // and to inline Apply() methods.

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, AddOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator +(ArithmeticCursor<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticCursor<TKey, TValue, AddOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, AddOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator +(TValue constant, ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            // Addition is commutative
            return new ArithmeticCursor<TKey, TValue, AddOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, NegateOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator -(ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticCursor<TKey, TValue, NegateOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, default(TValue));
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, PlusOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator +(ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticCursor<TKey, TValue, PlusOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, default(TValue));
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, SubtractOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator -(ArithmeticCursor<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticCursor<TKey, TValue, SubtractOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, SubtractReverseOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator -(TValue constant, ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticCursor<TKey, TValue, SubtractReverseOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator *(ArithmeticCursor<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator *(TValue constant, ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            // Multiplication is commutative
            return new ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, DivideOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator /(ArithmeticCursor<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticCursor<TKey, TValue, DivideOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, DivideReverseOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator /(TValue constant, ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticCursor<TKey, TValue, DivideReverseOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, ModuloOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator %(ArithmeticCursor<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticCursor<TKey, TValue, ModuloOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticCursor<TKey, TValue, ModuloReverseOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>> operator %(TValue constant, ArithmeticCursor<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticCursor<TKey, TValue, ModuloReverseOp<TValue>, ArithmeticCursor<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        #endregion Unary Operators
    }
}