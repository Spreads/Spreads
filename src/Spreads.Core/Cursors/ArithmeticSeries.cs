// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Spreads.DataTypes;

// ReSharper disable once CheckNamespace
namespace Spreads.Cursors
{
    public enum ArithmeticOp
    {
        /// <summary>
        /// Addition
        /// </summary>
        Add,

        /// <summary>
        /// Subtract a constant from series values
        /// </summary>
        Subtract,

        /// <summary>
        /// Subtract series values from a constant
        /// </summary>
        SubtractFrom,

        /// <summary>
        /// Multiply
        /// </summary>
        Multiply,

        /// <summary>
        /// Divide series values by a constant
        /// </summary>
        Divide,

        /// <summary>
        /// Divide a constant by series values
        /// </summary>
        DivideFrom,

        /// <summary>
        /// Modulo from series values division by a constant
        /// </summary>
        Modulo,

        /// <summary>
        /// Modulo from a constant division by series values
        /// </summary>
        ModuloFrom,

        /// <summary>
        /// Negate series values
        /// </summary>
        Negate,

        /// <summary>
        /// Power of series values by a constant
        /// </summary>
        Power,

        /// <summary>
        /// Power of a constant by series values
        /// </summary>
        PowerFrom,

        /// <summary>
        /// Unary +, nop.
        /// </summary>
        Plus
    }

    public interface IArithmeticOperation<T>
    {
        T Apply(T first, T second);
    }

    public struct AddOp<T> : IArithmeticOperation<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 + (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 + (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 + (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 + (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 + (decimal)(object)v2);
            }

            if (typeof(T) == typeof(Price))
            {
                return (T)(object)((Price)(object)v1 + (Price)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 + (dynamic)v2);
        }
    }


    public struct MultiplyOp<T> : IArithmeticOperation<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 * (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 * (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 * (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 * (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 * (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 * (dynamic)v2);
        }
    }

    /// <summary>
    /// A series that applies an arithmetic operation to each value of its input series. Specialized for input cursor.
    /// </summary>
    [Obsolete("Use the version with generic TOp")]
    public sealed class ArithmeticSeries<TKey, TValue, TCursor> :
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
            }
        }

        // NB this hurts, without it perf is 50% better [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("ReSharper", "RedundantCast")]
        [SuppressMessage("ReSharper", "RedundantOverflowCheckingContext")]
        private TValue Apply(TValue input)
        {
            if (typeof(TValue) == typeof(double))
            {
                var v1 = (double)(object)(input);
                var v2 = (double)(object)(_value);

                // NB Switch prevents inlining for some reason
                if (_op == ArithmeticOp.Add)
                    return (TValue)(object)(double)(v1 + v2);

                if (_op == ArithmeticOp.Subtract)
                    return (TValue)(object)(double)(v1 - v2);

                if (_op == ArithmeticOp.SubtractFrom)
                    return (TValue)(object)(double)(v2 - v1);

                if (_op == ArithmeticOp.Multiply)
                    return (TValue)(object)(double)(v1 * v2);

                if (_op == ArithmeticOp.Divide)
                    return (TValue)(object)(double)(v1 / v2);

                if (_op == ArithmeticOp.DivideFrom)
                    return (TValue)(object)(double)(v2 / v1);

                if (_op == ArithmeticOp.Modulo)
                    return (TValue)(object)(double)(v1 % v2);

                if (_op == ArithmeticOp.ModuloFrom)
                    return (TValue)(object)(double)(v2 % v1);

                if (_op == ArithmeticOp.Negate)
                    return (TValue)(object)(double)(-v1);

                if (_op == ArithmeticOp.Power)
                    return (TValue)(object)(double)Math.Pow(v1, v2);

                if (_op == ArithmeticOp.PowerFrom)
                    return (TValue)(object)(double)Math.Pow(v2, v1);

                if (_op == ArithmeticOp.Plus) return input;

                ThrowNotSupported();
                return default(TValue);
            }

            if (typeof(TValue) == typeof(float))
            {
                var v1 = (float)(object)(input);
                var v2 = (float)(object)(_value);

                // NB Switch prevents inlining for some reason
                if (_op == ArithmeticOp.Add)
                    return (TValue)(object)(float)(v1 + v2);

                if (_op == ArithmeticOp.Subtract)
                    return (TValue)(object)(float)(v1 - v2);

                if (_op == ArithmeticOp.SubtractFrom)
                    return (TValue)(object)(float)(v2 - v1);

                if (_op == ArithmeticOp.Multiply)
                    return (TValue)(object)(float)(v1 * v2);

                if (_op == ArithmeticOp.Divide)
                    return (TValue)(object)(float)(v1 / v2);

                if (_op == ArithmeticOp.DivideFrom)
                    return (TValue)(object)(float)(v2 / v1);

                if (_op == ArithmeticOp.Modulo)
                    return (TValue)(object)(float)(v1 % v2);

                if (_op == ArithmeticOp.ModuloFrom)
                    return (TValue)(object)(float)(v2 % v1);

                if (_op == ArithmeticOp.Negate)
                    return (TValue)(object)(float)(-v1);

                if (_op == ArithmeticOp.Power)
                    return (TValue)(object)(float)Math.Pow(v1, v2);

                if (_op == ArithmeticOp.PowerFrom)
                    return (TValue)(object)(float)Math.Pow(v2, v1);

                if (_op == ArithmeticOp.Plus) return input;

                ThrowNotSupported();
                return default(TValue);
            }

            if (typeof(TValue) == typeof(int))
            {
                var v1 = (int)(object)(input);
                var v2 = (int)(object)(_value);

                // NB Switch prevents inlining for some reason
                if (_op == ArithmeticOp.Add)
                    return (TValue)(object)(int)(v1 + v2);

                if (_op == ArithmeticOp.Subtract)
                    return (TValue)(object)(int)(v1 - v2);

                if (_op == ArithmeticOp.SubtractFrom)
                    return (TValue)(object)(int)(v2 - v1);

                if (_op == ArithmeticOp.Multiply)
                    return (TValue)(object)(int)(v1 * v2);

                if (_op == ArithmeticOp.Divide)
                    return (TValue)(object)(int)(v1 / v2);

                if (_op == ArithmeticOp.DivideFrom)
                    return (TValue)(object)(int)(v2 / v1);

                if (_op == ArithmeticOp.Modulo)
                    return (TValue)(object)(int)(v1 % v2);

                if (_op == ArithmeticOp.ModuloFrom)
                    return (TValue)(object)(int)(v2 % v1);

                if (_op == ArithmeticOp.Negate)
                    return (TValue)(object)(int)(-v1);

                if (_op == ArithmeticOp.Power)
                    return (TValue)(object)checked((int)Math.Pow(v1, v2));

                if (_op == ArithmeticOp.PowerFrom)
                    return (TValue)(object)checked((int)Math.Pow(v2, v1));

                if (_op == ArithmeticOp.Plus) return input;

                ThrowNotSupported();
                return default(TValue);
            }

            if (typeof(TValue) == typeof(long))
            {
                var v1 = (long)(object)(input);
                var v2 = (long)(object)(_value);

                // NB Switch prevents inlining for some reason
                if (_op == ArithmeticOp.Add)
                    return (TValue)(object)(long)(v1 + v2);

                if (_op == ArithmeticOp.Subtract)
                    return (TValue)(object)(long)(v1 - v2);

                if (_op == ArithmeticOp.SubtractFrom)
                    return (TValue)(object)(long)(v2 - v1);

                if (_op == ArithmeticOp.Multiply)
                    return (TValue)(object)(long)(v1 * v2);

                if (_op == ArithmeticOp.Divide)
                    return (TValue)(object)(long)(v1 / v2);

                if (_op == ArithmeticOp.DivideFrom)
                    return (TValue)(object)(long)(v2 / v1);

                if (_op == ArithmeticOp.Modulo)
                    return (TValue)(object)(long)(v1 % v2);

                if (_op == ArithmeticOp.ModuloFrom)
                    return (TValue)(object)(long)(v2 % v1);

                if (_op == ArithmeticOp.Negate)
                    return (TValue)(object)(long)(-v1);

                if (_op == ArithmeticOp.Power)
                    return (TValue)(object)checked((long)Math.Pow(v1, v2));

                if (_op == ArithmeticOp.PowerFrom)
                    return (TValue)(object)checked((long)Math.Pow(v2, v1));

                if (_op == ArithmeticOp.Plus) return input;

                ThrowNotSupported();
                return default(TValue);
            }

            if (typeof(TValue) == typeof(decimal))
            {
                var v1 = (decimal)(object)(input);
                var v2 = (decimal)(object)(_value);

                // NB Switch prevents inlining for some reason
                if (_op == ArithmeticOp.Add)
                    return (TValue)(object)(decimal)(v1 + v2);

                if (_op == ArithmeticOp.Subtract)
                    return (TValue)(object)(decimal)(v1 - v2);

                if (_op == ArithmeticOp.SubtractFrom)
                    return (TValue)(object)(decimal)(v2 - v1);

                if (_op == ArithmeticOp.Multiply)
                    return (TValue)(object)(decimal)(v1 * v2);

                if (_op == ArithmeticOp.Divide)
                    return (TValue)(object)(decimal)(v1 / v2);

                if (_op == ArithmeticOp.DivideFrom)
                    return (TValue)(object)(decimal)(v2 / v1);

                if (_op == ArithmeticOp.Modulo)
                    return (TValue)(object)(decimal)(v1 % v2);

                if (_op == ArithmeticOp.ModuloFrom)
                    return (TValue)(object)(decimal)(v2 % v1);

                if (_op == ArithmeticOp.Negate)
                    return (TValue)(object)(decimal)(-v1);

                if (_op == ArithmeticOp.Power)
                    return (TValue)(object)checked((decimal)Math.Pow((double)v1, (double)v2));

                if (_op == ArithmeticOp.PowerFrom)
                    return (TValue)(object)checked((decimal)Math.Pow((double)v1, (double)v2));

                if (_op == ArithmeticOp.Plus) return input;

                ThrowNotSupported();
                return default(TValue);
            }

            return ApplyDynamic(input);
        }

        private TValue ApplyDynamic(TValue input)
        {
            // NB this is 5-10 slower for doubles, but event for them it can process 10 Mops and "just works"

            var v1 = (dynamic)input;
            var v2 = (dynamic)_value;

            if (_op == ArithmeticOp.Add)
                return (TValue)(v1 + v2);

            if (_op == ArithmeticOp.Subtract)
                return (TValue)(v1 - v2);

            if (_op == ArithmeticOp.SubtractFrom)
                return (TValue)(v2 - v1);

            if (_op == ArithmeticOp.Multiply)
                return (TValue)(v1 * v2);

            if (_op == ArithmeticOp.Divide)
                return (TValue)(v1 / v2);

            if (_op == ArithmeticOp.DivideFrom)
                return (TValue)(v2 / v1);

            if (_op == ArithmeticOp.Modulo)
                return (TValue)(v1 % v2);

            if (_op == ArithmeticOp.ModuloFrom)
                return (TValue)(v2 % v1);

            if (_op == ArithmeticOp.Negate)
                return (TValue)(-v1);

            if (_op == ArithmeticOp.Power)
                return (TValue)(dynamic)Math.Pow((double)v1, (double)v2);

            if (_op == ArithmeticOp.PowerFrom)
                return (TValue)(dynamic)Math.Pow((double)v1, (double)v2);

            if (_op == ArithmeticOp.Plus) return +v1;

            ThrowNotSupported();
            return default(TValue);
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

        /// <summary>
        /// Get specialized enumerator.
        /// </summary>
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

        #region Unary Operators

        // NB This allows to combine arithmetic operators using sealed ArithmeticSeries<> as TCursor
        // and to inline Apply() methods.

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Add, constant);
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator +(TValue constant, ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            // Addition is commutative
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Add, constant);
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Negate, default(TValue));
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Plus, default(TValue));
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Subtract, constant);
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator -(TValue constant, ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.SubtractFrom, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator *(ArithmeticSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Multiply, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator *(TValue constant, ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            // Multiplication is commutative
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Multiply, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator /(ArithmeticSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Divide, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator /(TValue constant, ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.DivideFrom, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator %(ArithmeticSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Modulo, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator %(TValue constant, ArithmeticSeries<TKey, TValue, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.ModuloFrom, constant);
        }

        /// <summary>
        /// Power operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>> operator ^(ArithmeticSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TCursor>>(series, ArithmeticOp.Modulo, constant);
        }

        #endregion Unary Operators
    }

    /// <summary>
    /// A series that applies an arithmetic operation to each value of its input series. Specialized for input cursor.
    /// </summary>
    public sealed class ArithmeticSeries<TKey, TValue, TOp, TCursor> :
        CursorSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>,
        ICursor<TKey, TValue> //, ICanMapValues<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
        where TOp : IArithmeticOperation<TValue>
    {
        internal readonly TValue _value;
        internal readonly ISeries<TKey, TValue> _series;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal ArithmeticSeries(ISeries<TKey, TValue> series, TValue value)
        {
            _series = series;
            _cursor = GetCursor<TKey, TValue, TCursor>(_series);
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
                var mapped = new ArithmeticSeries<TKey, TValue, TOp, TCursor>(batch, _value);
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
                return default(TOp).Apply(_cursor.CurrentValue, _value);
            }
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
        public override ArithmeticSeries<TKey, TValue, TOp, TCursor> Clone()
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
        public override ArithmeticSeries<TKey, TValue, TOp, TCursor> Create()
        {
            if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            {
                State = CursorState.Initialized;
                return this;
            }
            var clone = new ArithmeticSeries<TKey, TValue, TOp, TCursor>(_series, _value);
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
            return default(TOp).Apply(_cursor.Source.GetAt(idx), _value);
        }

        /// <summary>
        /// Get specialized enumerator.
        /// </summary>
        public new ArithmeticSeries<TKey, TValue, TOp, TCursor> GetEnumerator()
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

        #region Unary Operators

        // NB This allows to combine arithmetic operators using sealed ArithmeticSeries<> as TCursor
        // and to inline Apply() methods.

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Add, constant);
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            // Addition is commutative
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Add, constant);
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Negate, default(TValue));
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Plus, default(TValue));
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Subtract, constant);
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.SubtractFrom, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator *(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Multiply, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator *(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            // Multiplication is commutative
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Multiply, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator /(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Divide, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator /(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.DivideFrom, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator %(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Modulo, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator %(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.ModuloFrom, constant);
        }

        /// <summary>
        /// Power operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator ^(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, ArithmeticOp.Modulo, constant);
        }

        #endregion Unary Operators
    }
}