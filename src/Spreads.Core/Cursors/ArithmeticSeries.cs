// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads.Cursors
{
    public enum ArithmeticOp
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        Negate,
        Power
        // TODO
    }

    /// <summary>
    /// A series that applies an arithmetic operation to each value of its input series. Specialized for input cursor.
    /// </summary>
    public class ArithmeticSeries<TKey, TValue, TCursor> :
        CursorSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>,
        ICursor<TKey, TValue> //, ICanMapValues<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
    {
        internal readonly ArithmeticOp _op;
        internal readonly TValue _value;
        internal readonly ISeries<TKey, TValue> _series;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal ArithmeticSeries(ISeries<TKey, TValue> series, ArithmeticOp op, TValue value)
        {
            _series = series;
            _cursor = GetCursor<TKey, TValue, TCursor>(_series);
            _op = op;
            _value = value;
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch
        {
            get
            {
                var batch = _cursor.CurrentBatch;
                // TODO when batching is proper implemented (nested batches) reuse an instance for this
                var mapped = new ArithmeticSeries<TKey, TValue, TCursor>(batch, _op, _value);
                return mapped;
            }
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
                return Apply(_cursor.CurrentValue);
                //if (typeof(TValue) == typeof(double))
                //{
                //    var v1 = (double)(object)(_cursor.CurrentValue);
                //    var v2 = (double)(object)(_value);

                //    if (_op == ArithmeticOp.Add) return (TValue)(object)(v1 + v2);

                //    if (_op == ArithmeticOp.Subtract)
                //        return (TValue)(object)(v1 - v2);

                //    if (_op == ArithmeticOp.Multiply)
                //        return (TValue)(object)(v1 * v2);

                //    if (_op == ArithmeticOp.Divide)
                //        return (TValue)(object)(v1 / v2);

                //    if (_op == ArithmeticOp.Modulo)
                //        return (TValue)(object)(v1 % v2);

                //    if (_op == ArithmeticOp.Negate)
                //        return (TValue)(object)(-v1);

                //    if (_op == ArithmeticOp.Power)
                //        return (TValue)(object)(Math.Pow(v1, v2));

                //}
                ////ThrowNotSupported();
                //return (default(TValue));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TValue Apply(TValue input)
        {
            if (typeof(TValue) == typeof(double))
            {
                var v1 = (double)(object)(input);
                var v2 = (double)(object)(_value);
                if (_op == ArithmeticOp.Add) return (TValue)(object)(v1 + v2);

                if (_op == ArithmeticOp.Subtract)
                    return (TValue)(object)(v1 - v2);

                if (_op == ArithmeticOp.Multiply)
                    return (TValue)(object)(v1 * v2);

                if (_op == ArithmeticOp.Divide)
                    return (TValue)(object)(v1 / v2);

                if (_op == ArithmeticOp.Modulo)
                    return (TValue)(object)(v1 % v2);

                if (_op == ArithmeticOp.Negate)
                    return (TValue)(object)(-v1);

                if (_op == ArithmeticOp.Power)
                    return (TValue)(object)(Math.Pow(v1, v2));
            }

            //ThrowNotSupported();
            return (default(TValue));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotSupported()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public override bool IsIndexed => _series.IsIndexed;

        /// <inheritdoc />
        public override bool IsReadOnly => _series.IsReadOnly;

        /// <inheritdoc />
        public override Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public override ArithmeticSeries<TKey, TValue, TCursor> Clone()
        {
            var clone = Create();
            Debug.Assert(clone.State == CursorState.Initialized);
            if (State == CursorState.Moving)
            {
                clone.MoveAt(CurrentKey, Lookup.EQ);
            }
            return clone;
        }

        /// <inheritdoc />
        public override ArithmeticSeries<TKey, TValue, TCursor> Create()
        {
            if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            {
                State = CursorState.Initialized;
                return this;
            }
            var clone = new ArithmeticSeries<TKey, TValue, TCursor>(_series, _op, _value);
            clone.State = CursorState.Initialized;
            return clone;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public override TValue GetAt(int idx)
        {
            return Apply(_cursor.Source.GetAt(idx));
        }

        public new ArithmeticSeries<TKey, TValue, TCursor> GetEnumerator()
        {
            var clone = Create();
            return clone;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = Apply(v);
                return true;
            }
            value = default(TValue);
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            var moved = _cursor.MoveAt(key, direction);
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            var moved = _cursor.MoveFirst();
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            var moved = _cursor.MoveLast();
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if ((int)State < (int)CursorState.Moving) return MoveFirst();
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            var moved = await _cursor.MoveNextBatch(cancellationToken);
            if (moved)
            {
                State = CursorState.BatchMoving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if ((int)State < (int)CursorState.Moving) return MoveLast();
            return _cursor.MovePrevious();
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

        //BaseSeries<TKey, TValue1> ICanMapValues<TKey, TValue>.Map<TValue1>(Func<TValue, TValue1> selector, Func<Buffer<TValue>, Buffer<TValue1>> batchSelector)
        //{
        //    if (batchSelector != null)
        //    {
        //        throw new NotImplementedException();
        //    }
        //    return new MapValuesSeries<TKey, TValue, TValue1, TCursor>(_series, CoreUtils.CombineMaps<TValue, TValue, TValue1>(Apply, selector));
        //}
    }
}