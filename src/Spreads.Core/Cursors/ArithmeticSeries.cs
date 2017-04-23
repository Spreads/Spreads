// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
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
    public struct AddOp<T> : IOp<T>
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

    public struct MultiplyOp<T> : IOp<T>
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

    public struct SubtractOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 - (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 - (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 - (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 - (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 - (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 - (dynamic)v2);
        }
    }

    public struct SubtractReverseOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v2, T v1) // reversed v1 and v2
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 - (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 - (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 - (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 - (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 - (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 - (dynamic)v2);
        }
    }

    public struct DivideOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 / (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 / (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 / (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 / (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 / (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 / (dynamic)v2);
        }
    }

    public struct DivideReverseOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 / (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 / (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 / (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 / (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 / (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 / (dynamic)v2);
        }
    }

    public struct ModuloOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 % (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 % (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 % (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 % (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 % (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 % (dynamic)v2);
        }
    }

    public struct ModuloReverseOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v2, T v1)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)v1 % (double)(object)v2);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)v1 % (float)(object)v2);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)v1 % (int)(object)v2);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)v1 % (long)(object)v2);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)v1 % (decimal)(object)v2);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)((dynamic)v1 % (dynamic)v2);
        }
    }

    public struct NegateOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)(-(double)(object)v1);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)(-(float)(object)v1);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)(-(int)(object)v1);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)(-(long)(object)v1);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)(-(decimal)(object)v1);
            }

            return ApplyDynamic(v1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)(-(dynamic)v1);
        }
    }

    public struct PlusOp<T> : IOp<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Apply(T v1, T v2)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)(+(double)(object)v1);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)(+(float)(object)v1);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)(+(int)(object)v1);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)(+(long)(object)v1);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)(+(decimal)(object)v1);
            }

            return ApplyDynamic(v1, v2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ApplyDynamic(T v1, T v2)
        {
            // NB this is 5-10 slower for doubles, but even for them it can process 10 Mops and "just works"
            return (T)(+(dynamic)v1);
        }
    }

    internal sealed class UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> :
        CursorSeries<TKey, TResult, UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor>>,
        ICursor<TKey, TResult> //, ICanMapValues<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
        where TOp : struct, IOp<TValue, TValue2, TResult>
    {
        internal readonly TValue2 _value;
        internal readonly ISeries<TKey, TValue> _series;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal UnaryOpSeries(ISeries<TKey, TValue> series, TValue2 value)
        {
            _series = series;
            _value = value;
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TResult> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TResult>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TResult> CurrentBatch
        {
            get
            {
                var batch = _cursor.CurrentBatch;
                // TODO when batching is proper implemented (nested batches) reuse an instance for this
                var mapped = new UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor>(batch, _value);
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
        public TResult CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return default(TOp).Apply(_cursor.CurrentValue, _value);
            }
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
        public override UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> Clone()
        {
            var clone = Initialize();
            Debug.Assert(clone.State == CursorState.Initialized);
            if (State == CursorState.Moving)
            {
                clone.MoveAt(CurrentKey, Lookup.EQ);
            }
            return clone;
        }

        /// <inheritdoc />
        public override UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> Initialize()
        {
            if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            {
                _cursor = GetCursor<TKey, TValue, TCursor>(_series);
                State = CursorState.Initialized;
                return this;
            }
            var clone = new UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor>(_series, _value);
            // NB recursive call but it should always hit the if case above
            var initialized = clone.Initialize();
            Debug.Assert(ReferenceEquals(clone, initialized));
            return initialized;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        /// <inheritdoc />
        public override TResult GetAt(int idx)
        {
            return default(TOp).Apply(_cursor.Source.GetAt(idx), _value);
        }

        /// <summary>
        /// Get specialized enumerator.
        /// </summary>
        public new UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> GetEnumerator()
        {
            var clone = Initialize();
            return clone;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TResult value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = default(TOp).Apply(v, _value);
                return true;
            }
            value = default(TResult);
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

        ICursor<TKey, TResult> ICursor<TKey, TResult>.Clone()
        {
            return Clone();
        }
    }

    /// <summary>
    /// A series that applies an arithmetic operation to each value of its input series. Specialized for input cursor.
    /// </summary>
    public sealed class ArithmeticSeries<TKey, TValue, TOp, TCursor> :
        CursorSeries<TKey, TValue, ArithmeticSeries<TKey, TValue, TOp, TCursor>>,
        ICursor<TKey, TValue> //, ICanMapValues<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
        where TOp : struct, IOp<TValue>
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
            var clone = Initialize();
            Debug.Assert(clone.State == CursorState.Initialized);
            if (State == CursorState.Moving)
            {
                clone.MoveAt(CurrentKey, Lookup.EQ);
            }
            return clone;
        }

        /// <inheritdoc />
        public override ArithmeticSeries<TKey, TValue, TOp, TCursor> Initialize()
        {
            if (State == CursorState.None && ThreadId == Environment.CurrentManagedThreadId)
            {
                _cursor = GetCursor<TKey, TValue, TCursor>(_series);
                State = CursorState.Initialized;
                return this;
            }
            var clone = new ArithmeticSeries<TKey, TValue, TOp, TCursor>(_series, _value);
            // NB recursive call but it should always hit the if case above
            var initialized = clone.Initialize();
            Debug.Assert(ReferenceEquals(clone, initialized));
            return initialized;
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
            var clone = Initialize();
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
        public static ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            // Addition is commutative
            return new ArithmeticSeries<TKey, TValue, AddOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, NegateOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, NegateOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, default(TValue));
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, PlusOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator +(ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, PlusOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, default(TValue));
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, SubtractOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, SubtractOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, SubtractReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator -(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, SubtractReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator *(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator *(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            // Multiplication is commutative
            return new ArithmeticSeries<TKey, TValue, MultiplyOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, DivideOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator /(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, DivideOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, DivideReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator /(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, DivideReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ModuloOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator %(ArithmeticSeries<TKey, TValue, TOp, TCursor> series, TValue constant)
        {
            return new ArithmeticSeries<TKey, TValue, ModuloOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static ArithmeticSeries<TKey, TValue, ModuloReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>> operator %(TValue constant, ArithmeticSeries<TKey, TValue, TOp, TCursor> series)
        {
            return new ArithmeticSeries<TKey, TValue, ModuloReverseOp<TValue>, ArithmeticSeries<TKey, TValue, TOp, TCursor>>(series, constant);
        }

        #endregion Unary Operators
    }
}