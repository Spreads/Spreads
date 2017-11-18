// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors.Experimental
{
    /// <summary>
    /// A <see cref="AbstractCursorSeries{TKey,TValue,TCursor}"/> that applies an arithmetic operation to each value of its input series.
    /// </summary>
    [Obsolete("Use CursorSeries")]
    internal sealed class ArithmeticSeries<TKey, TValue, TOp, TCursor> :
        AbstractCursorSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>,
        ISpecializedCursor<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> // TODO , ICanMapValues<TKey, TValue>
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

        #endregion Cursor state

        #region Constructors

        /// <summary>
        /// ArithmeticSeries constructor.
        /// </summary>
        public ArithmeticSeries() { }

        // TODO private
        internal ArithmeticSeries(TCursor cursor, TValue value) : this()
        {
            _value = value;
            _cursor = cursor;
        }

        internal static ArithmeticSeries<TKey, TValue, TOp, TCursor> Create(TCursor cursor, TValue value)
        {
            var instance = GetUninitializedStatic(); //
            instance._value = value;
            instance._cursor = cursor;
            return instance;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public ArithmeticSeries<TKey, TValue, TOp, TCursor> Clone()
        {
            var instance = GetUninitializedInstance();
            if (ReferenceEquals(instance, this))
            {
                // was not in use
                return this;
            }
            instance._cursor = _cursor.Clone();
            instance._value = _value;
            instance.State = State;
            return instance;
        }

        /// <inheritdoc />
        public override ArithmeticSeries<TKey, TValue, TOp, TCursor> Initialize()
        {
            var instance = GetUninitializedInstance();
            instance._cursor = _cursor.Initialize();
            instance._value = _value;
            instance.State = CursorState.Initialized;
            return instance;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // when _inUse == 1 then we own this instance until release
            if (Volatile.Read(ref _inUse) == 1)
            {
                // NB keep cursor state for reuse
                // dispose is called on the result of Initialize(), the cursor from
                // constructor could be uninitialized but contain some state, e.g. _value for this ArithmeticSeries
                _cursor.Dispose();
            }
            else
            {
                // NB do not dispose in the terminal case
                // _cursor could be referenced from other places (it was provided in a ctor, we do not own it,
                // we only own the result of Initialize() call)), disposing it could make it unusable.
                // Disposing in the case above is safe because _cursor was returned by Initialize()
                // method (_inUse for CursorSeries, always a new instance for other cursors).
                _cursor = default(TCursor);
            }
            // TODO check state in MN/MF/etc, we could dispose an active cursor
            State = CursorState.None;
            ReleaseInstance(this);
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
        public IReadOnlySeries<TKey, TValue> CurrentBatch
        {
            get
            {
                var batch = _cursor.CurrentBatch;
                // TODO when batching is proper implemented (nested batches) reuse an instance for this
                var mapped = new ArithmeticSeries<TKey, TValue, TOp, Cursor<TKey, TValue>>(new Cursor<TKey, TValue>(batch.GetCursor()), _value);
                return mapped;
            }
        }

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

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
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
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
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
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
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
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
            //var moved = await _cursor.MoveNextBatch(cancellationToken);
            //if (moved)
            //{
            //    State = CursorState.BatchMoving;
            //}
            //return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();
            return _cursor.MovePrevious();
        }

        #endregion ICursor members

        #region Series overrides

        /// <inheritdoc />
        public override bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public override bool IsReadOnly => _cursor.Source.IsReadOnly;

        /// <inheritdoc />
        public override bool IsEmpty => _cursor.Source.IsEmpty;

        /// <inheritdoc />
        public override Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public override TValue GetAt(int idx)
        {
            return default(TOp).Apply(_cursor.Source.GetAt(idx), _value);
        }

        #endregion Series overrides

        //Series<TKey, TValue1> ICanMapValues<TKey, TValue>.Map<TValue1>(Func<TValue, TValue1> selector, Func<Buffer<TValue>, Memory<TValue1>> batchSelector)
        //{
        //    if (batchSelector != null)
        //    {
        //        throw new NotImplementedException();
        //    }
        //    return new MapValuesSeries<TKey, TValue, TValue1, TCursor>(_series, CoreUtils.CombineMaps<TValue, TValue, TValue1>(Apply, selector));
        //}

        #region Unary Operators

        // NB This allows to combine arithmetic operators using sealed ArithmeticSeries<> as TCursor
        // and to inline Apply() methods.

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            // Addition is commutative
            return ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, NegateOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return ArithmeticSeries<TKey, TValue, NegateOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, default(TValue));
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, PlusOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return ArithmeticSeries<TKey, TValue, PlusOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, default(TValue));
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, SubtractOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return ArithmeticSeries<TKey, TValue, SubtractOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, SubtractReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return ArithmeticSeries<TKey, TValue, SubtractReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator *(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator *(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            // Multiplication is commutative
            return ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, DivideOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator /(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return ArithmeticSeries<TKey, TValue, DivideOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, DivideReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator /(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return ArithmeticSeries<TKey, TValue, DivideReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ModuloOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator %(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return ArithmeticSeries<TKey, TValue, ModuloOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ModuloReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator %(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return ArithmeticSeries<TKey, TValue, ModuloReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>.Create(series, constant);
        }

        #endregion Unary Operators
    }
}