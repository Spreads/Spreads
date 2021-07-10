//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Threading;
//using System.Threading.Tasks;
//using Spreads.Collections;

//namespace Spreads.Deprecated
//{

//    // TODO The only valuebale thing left in this file is operators, move them to Series and delete this file/folder.

//    /// <summary>
//    /// Base generic class for all series implementations.
//    /// </summary>
//    /// <typeparam name="TKey">Type of series keys.</typeparam>
//    /// <typeparam name="TValue">Type of series values.</typeparam>
//#pragma warning disable 660, 661

//    public abstract class Series<TKey, TValue> : BaseContainer,
//        ISeries<TKey, TValue, Cursor<TKey, TValue>>, IAsyncCompleter, IDisposable
//#pragma warning restore 660, 661
//    {
//        protected abstract ICursor<TKey, TValue> GetCursorImpl();

//        /// <inheritdoc />
//        public abstract KeyComparer<TKey> Comparer { get; }

//        /// <inheritdoc />
//        public abstract bool IsIndexed { get; }

//        /// <inheritdoc />
//        public abstract bool IsCompleted { get; }

//        // Abstract members do not match enumerator patterns so that derived classes could have
//        // faster struct/strongly types enumerators

//        protected abstract IAsyncEnumerator<KeyValuePair<TKey, TValue>> GetAsyncEnumeratorImpl();

//        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetAsyncEnumerator(CancellationToken ct = default)
//        {
//#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
//            return GetAsyncEnumeratorImpl();
//#pragma warning restore HAA0401 // Possible allocation of reference type enumerator
//        }

//        protected abstract IEnumerator<KeyValuePair<TKey, TValue>> GetEnumeratorImpl();

//        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
//        {
//#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
//            return GetEnumeratorImpl();
//#pragma warning restore HAA0401 // Possible allocation of reference type enumerator
//        }

//        IEnumerator IEnumerable.GetEnumerator()
//        {
//#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
//            return GetEnumeratorImpl();
//#pragma warning restore HAA0401 // Possible allocation of reference type enumerator
//        }

//        Cursor<TKey, TValue> ISeries<TKey, TValue, Cursor<TKey, TValue>>.GetCursor()
//        {
//            return GetCursor();
//        }

//        ICursor<TKey, TValue> ISeries<TKey, TValue>.GetCursor()
//        {
//            return GetCursorImpl();
//        }

//        /// <inheritdoc />
//        public abstract Opt<KeyValuePair<TKey, TValue>> First { get; }

//        /// <inheritdoc />
//        public abstract Opt<KeyValuePair<TKey, TValue>> Last { get; }

//        public TValue LastValueOrDefault => throw new NotImplementedException();

//        /// <inheritdoc />
//        public TValue this[TKey key]
//        {
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get
//            {
//                if (TryGetValue(key, out var value))
//                {
//                    return value;
//                }
//                ThrowHelper.ThrowKeyNotFoundException("Key not found in series");
//                return default;
//            }
//        }

//        /// <inheritdoc />
//        public abstract bool TryGetValue(TKey key, out TValue value);

//        /// <inheritdoc />
//        public abstract bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

//        /// <inheritdoc />
//        public virtual bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp)
//        {
//            if (index < 0)
//            {
//                ThrowHelper.ThrowNotImplementedException("TODO Support negative indexes in TryGetAt");
//            }
//            // TODO (review) not so stupid and potentially throwing impl
//            try
//            {
//                kvp = this.Skip(Math.Max(0, checked((int)(index)) - 1)).First();
//                return true;
//            }
//            catch
//            {
//                kvp = default;
//                return false;
//            }
//        }

//        /// <inheritdoc />
//        public virtual IEnumerable<TKey> Keys => this.Select(kvp => kvp.Key);

//        /// <inheritdoc />
//        public virtual IEnumerable<TValue> Values => this.Select(kvp => kvp.Value);

//        public Cursor<TKey, TValue> GetCursor()
//        {
//            return new Cursor<TKey, TValue>(GetCursorImpl());
//        }

//        #region Implicit cast

//        /// <summary>
//        /// Implicitly convert <see cref="Series{TKey,TValue}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
//        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
//        /// </summary>
//        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(Series<TKey, TValue> series)
//        {
//            var c = series.GetCursor();
//            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
//        }

//        #endregion Implicit cast

//        #region Unary Operators

//        // UNARY ARITHMETIC

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
//            +(Series<TKey, TValue> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
//            +(TValue constant, Series<TKey, TValue> series)
//        {
//            // Addition is commutative
//            var cursor = new Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Negate operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>> operator
//            -(Series<TKey, TValue> series)
//        {
//            var cursor =
//                new Op<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), default);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Unary plus operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>> operator
//            +(Series<TKey, TValue> series)
//        {
//            var cursor =
//                new Op<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), default);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>> operator
//            -(Series<TKey, TValue> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>> operator
//            -(TValue constant, Series<TKey, TValue> series)
//        {
//            var cursor =
//                new Op<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
//            *(Series<TKey, TValue> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
//            *(TValue constant, Series<TKey, TValue> series)
//        {
//            // Multiplication is commutative
//            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>> operator
//            /(Series<TKey, TValue> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>> operator
//            /(TValue constant, Series<TKey, TValue> series)
//        {
//            var cursor =
//                new Op<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>> operator
//            %(Series<TKey, TValue> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>> operator
//            %(TValue constant, Series<TKey, TValue> series)
//        {
//            var cursor =
//                new Op<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetCursor(), constant);
//            return cursor.Source;
//        }

//        // UNARY LOGIC

//        /// <summary>
//        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            ==(Series<TKey, TValue> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    EQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            ==(TValue comparand, Series<TKey, TValue> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    EQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            !=(Series<TKey, TValue> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    NEQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            !=(TValue comparand, Series<TKey, TValue> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    NEQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            <(Series<TKey, TValue> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    LTOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            <(TValue comparand, Series<TKey, TValue> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    LTReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            >(Series<TKey, TValue> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    GTOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            >(TValue comparand, Series<TKey, TValue> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    GTReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            <=(Series<TKey, TValue> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    LEOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            <=(TValue comparand, Series<TKey, TValue> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    LEReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator >=(
//            Series<TKey, TValue> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    GEOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
//            >=(TValue comparand, Series<TKey, TValue> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetCursor(), comparand,
//                    GEReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        #endregion Unary Operators

//        #region Binary Operators

//        // BINARY ARITHMETIC

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            +(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            // TODO change to Op2, measure result
//            //var op = zipCursor.Apply<AddOp<TValue>>();
//            //return op;
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            +(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            +(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();
//            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            -(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            -(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            -(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();
//            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            *(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            *(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            *(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();
//            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            /(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            /(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            /(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();
//            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            %(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            %(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();
//            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            %(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();
//            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(selector).Source;
//        }

//        // BINARY LOGIC

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            ==(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            ==(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            ==(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            !=(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            !=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            !=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            <=(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            <=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            <=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            >=(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            >=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            >=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            <(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            <(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            <(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            >(Series<TKey, TValue> series, Series<TKey, TValue> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetCursor();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            >(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
//        {
//            if (other is null) throw new ArgumentNullException(nameof(other));
//            var c1 = series.GetEnumerator();
//            var c2 = other.GetCursor();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
//                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
//            >(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var c1 = series.GetCursor();
//            var c2 = other.GetEnumerator();

//            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
//            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
//        }

//        #endregion Binary Operators

       
//        void IDisposable.Dispose()
//        {
//            Dispose(true);
//            GC.SuppressFinalize(this);
//        }

//        System.Collections.Generic.IAsyncEnumerator<KeyValuePair<TKey, TValue>> System.Collections.Generic.IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetAsyncEnumerator(CancellationToken cancellationToken)
//        {
//            throw new NotImplementedException();
//        }

//        public ContainerLayout ContainerLayout => throw new NotImplementedException();

//        public Mutability Mutability => throw new NotImplementedException();

//        public KeySorting KeySorting => throw new NotImplementedException();

//        public ulong? RowCount => throw new NotImplementedException();

//        public bool IsEmpty => throw new NotImplementedException();
//    }

//    /// <summary>
//    /// Base class for collections (containers).
//    /// </summary>
//#pragma warning disable 660, 661

//    public abstract class ContainerSeries<TKey, TValue, TCursor> : Series<TKey, TValue>,
//        ISeries<TKey, TValue, TCursor>
//#pragma warning restore 660, 661
//        where TCursor : ICursor<TKey, TValue, TCursor>
//    {
//        [Obsolete("use _ac")]
//        internal long Locker;

//        // ReSharper restore InconsistentNaming
//        internal bool _isSynchronized = true; // todo _ac == default? or MutabilityEnum and always synchronize if not immutable?

//        internal bool _isReadOnly; // TODO use Mutability enum

//        /// <inheritdoc />
//        public override bool IsCompleted
//        {
//            // NB this is set only inside write lock, no other locks are possible
//            // after this value is set so we do not need read lock. This is very
//            // hot path for MNA
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get => Volatile.Read(ref _isReadOnly);
//        }

//        protected sealed override IAsyncEnumerator<KeyValuePair<TKey, TValue>> GetAsyncEnumeratorImpl()
//        {
//            if (IsCompleted)
//            {
//#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
//                return GetAsyncCursor();
//#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
//            }
//            var c = new AsyncCursor<TKey, TValue, TCursor>(GetCursor(), true);
//            return c;
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public AsyncCursor<TKey, TValue, TCursor> GetAsyncEnumerator()
//        {
//            var c = new AsyncCursor<TKey, TValue, TCursor>(GetCursor(), false);
//            return c;
//        }

//        // TODO should be nonvirt method with concrete type, try to rework parent
//        protected sealed override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumeratorImpl()
//        {
//#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
//            return GetCursor();
//#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
//        }

//        public TCursor GetEnumerator()
//        {
//            return GetCursor();
//        }

//        protected override ICursor<TKey, TValue> GetCursorImpl()
//        {
//            if (IsCompleted)
//            {
//#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
//                return GetCursor();
//#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
//            }
//            // NB subscribe from AsyncCursor
//            var c = new AsyncCursor<TKey, TValue, TCursor>(GetCursor());
//            return c;
//        }

//        internal abstract TCursor GetContainerCursor();

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public TCursor GetCursor()
//        {
//            return GetContainerCursor();
//        }

//        public AsyncCursor<TKey, TValue, TCursor> GetAsyncCursor()
//        {
//            throw new NotImplementedException();
//        }

//        #region Synchronization

//        internal Task DoComplete()
//        {
//#pragma warning disable 618
//            BeforeWrite();
//#pragma warning restore 618
//            if (!_isReadOnly)
//            {
//                _isReadOnly = true;
//                _isSynchronized = false;
//                // NB this is API design quirk: AfterWrite checks for _isSynchronized
//                // and ignores all further logic if that is false, but BeforeWrite
//                // always increments _nextVersion when _isSynchronized = true
//                // We have it only here, ok for now but TODO review later
//                NextVersion = Version;
//            }
//#pragma warning disable 618
//            AfterWrite(false);
//#pragma warning restore 618
//            Interlocked.Exchange(ref Locker, 0L);
//            NotifyUpdate();
//            return Task.CompletedTask;
//        }

//        #endregion Synchronization

//        #region Unary Operators

//        // UNARY ARITHMETIC

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
//            +(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Add operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
//            +(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            // Addition is commutative
//            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Negate operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, TCursor>> operator
//            -(ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            var cursor = new Op<TKey, TValue, NegateOp<TValue>, TCursor>(series.GetContainerCursor(), default);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Unary plus operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, TCursor>> operator
//            +(ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            var cursor = new Op<TKey, TValue, PlusOp<TValue>, TCursor>(series.GetContainerCursor(), default);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, TCursor>> operator
//            -(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Subtract operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>> operator
//            -(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            var cursor =
//                new Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
//            *(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Multiply operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
//            *(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            // Multiplication is commutative
//            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, TCursor>> operator
//            /(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, DivideOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Divide operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>> operator
//            /(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            var cursor = new Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, TCursor>> operator
//            %(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
//        {
//            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Modulo operator.
//        /// </summary>
//        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>> operator
//            %(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            var cursor = new Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
//            return cursor.Source;
//        }

//        // UNARY LOGIC

//        /// <summary>
//        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            ==(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, EQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            ==(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, EQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            !=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, NEQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            !=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, NEQOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            <(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, LTOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            <(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
//                LTReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            >(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, GTOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            >(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
//                GTReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            <=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, LEOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            <=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
//                LEReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            >=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor =
//                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, GEOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        /// <summary>
//        /// Comparison operator.
//        /// </summary>
//        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
//            >=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            if (series is null) throw new ArgumentNullException(nameof(series));
//            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
//                GEReverseOp<TValue>.Instance);
//            return cursor.Source;
//        }

//        #endregion Unary Operators

//        #region Binary Operators

//        // BINARY ARITHMETIC

//        ///// <summary>
//        ///// Add operator.
//        ///// </summary>
//        //public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
//        //    +(ContainerSeries<TKey, TValue, TCursor> series, ContainerSeries<TKey, TValue, TCursor> other)
//        //{
//        //    var c1 = series.GetContainerCursor();
//        //    var c2 = other.GetContainerCursor();
//        //    //Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

//        //    var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
//        //    return zipCursor.Map(AddOp<TValue>.ZipSelector).Source;

//        //    //var op2 = new Op2<TKey, TValue, AddOp<TValue>, Zip<TKey, TValue, TValue, TCursor, TCursor>>(zipCursor);

//        //    //return op2.Source;
//        //}

//        #endregion Binary Operators

//        #region Implicit cast

//        /// <summary>
//        /// Implicitly convert <see cref="Series{TKey,TValue}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
//        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
//        /// </summary>
//        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(
//            ContainerSeries<TKey, TValue, TCursor> series)
//        {
//            var c = new Cursor<TKey, TValue>(series.GetCursorImpl());
//            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
//        }

//        #endregion Implicit cast
//    }

//    /// <summary>
//    /// Base class for collections (containers) with <see cref="ISeries{TKey,TValue}"/> members implemented via a cursor.
//    /// </summary>
//    public abstract class CursorContainerSeries<TKey, TValue, TCursor> : ContainerSeries<TKey, TValue, TCursor>, IMutableSeries<TKey, TValue>, IDisposable
//#pragma warning restore 660, 661
//        where TCursor : ICursor<TKey, TValue, TCursor>
//#pragma warning restore 660, 661
//    {
//        private bool _cursorIsSet;
//        private TCursor _c;

//        private object _syncRoot;

//        /// <summary>
//        /// An object for external synchronization.
//        /// </summary>
//        public object SyncRoot
//        {
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get
//            {
//                if (_syncRoot == null)
//                {
//                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
//                }
//                return _syncRoot;
//            }
//        }

//        private ref TCursor C
//        {
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get
//            {
//                lock (SyncRoot)
//                {
//                    if (!_cursorIsSet)
//                    {
//                        _c = GetContainerCursor();
//                        _cursorIsSet = true;
//                    }

//                    return ref _c;
//                }
//            }
//        }

//        #region ISeries members

//        /// <inheritdoc />
//        public override Opt<KeyValuePair<TKey, TValue>> First
//        {
//            get
//            {
//                lock (SyncRoot)
//                {
//                    return C.MoveFirst() ? Opt.Present(C.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
//                }
//            }
//        }

//        /// <inheritdoc />
//        public override Opt<KeyValuePair<TKey, TValue>> Last
//        {
//            get
//            {
//                lock (SyncRoot)
//                {
//                    return C.MoveLast() ? Opt.Present(C.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
//                }
//            }
//        }

//        public TValue this[TKey key]
//        {
//            get => throw new NotImplementedException();
//            set => throw new NotImplementedException();
//        }

//        public override bool TryGetValue(TKey key, out TValue value)
//        {
//            lock (SyncRoot)
//            {
//                return C.TryGetValue(key, out value);
//            }
//        }

//        /// <inheritdoc />
//        public override bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp)
//        {
//            // NB call to this.NavCursor.Source.TryGetAt(idx) is recursive (=> SO) and is logically wrong
//            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
//            lock (SyncRoot)
//            {
//                if (!C.MoveFirst())
//                {
//                    throw new KeyNotFoundException();
//                }
//                for (var i = 0; i < index - 1; i++)
//                {
//                    if (!C.MoveNext())
//                    {
//                        kvp = default;
//                        return false;
//                    }
//                }
//                kvp = C.Current;
//                return true;
//            }
//        }

//        /// <inheritdoc />
//        public override bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
//        {
//            lock (SyncRoot)
//            {
//                if (C.MoveAt(key, direction))
//                {
//                    kvp = C.Current;
//                    return true;
//                }

//                kvp = default;
//                return false;
//            }
//        }

//        /// <inheritdoc />
//        public override IEnumerable<TKey> Keys
//        {
//            get
//            {
//                lock (SyncRoot)
//                {
//                    while (C.MoveNext())
//                    {
//                        yield return C.CurrentKey;
//                    }
//                }
//            }
//        }

//        /// <inheritdoc />
//        public override IEnumerable<TValue> Values
//        {
//            get
//            {
//                lock (SyncRoot)
//                {
//                    while (C.MoveNext())
//                    {
//                        yield return C.CurrentValue;
//                    }
//                }
//            }
//        }

//        #endregion ISeries members

//        protected override void Dispose(bool disposing)
//        {
//            lock (SyncRoot)
//            {
//                if (!_cursorIsSet)
//                {
//                    return;
//                }
//                _cursorIsSet = false;
//                _c.Dispose();
//            }
//            base.Dispose(disposing);
//        }

//        public void Dispose()
//        {
//            GC.SuppressFinalize(this);
//            Dispose(true);
//        }

//        ~CursorContainerSeries()
//        {
//            Console.WriteLine($"CursorContainerSeries is finalized");
//            Dispose(false);
//        }

//        public abstract long Count { get; }

//        public abstract bool IsAppendOnly { get; }

//        public abstract bool Set(TKey key, TValue value);
//        public void Set<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public abstract bool TryAdd(TKey key, TValue value);
//        public void Add(TKey key, TValue value)
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryAdd<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public void Add<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryPrepend(TKey key, TValue value)
//        {
//            throw new NotImplementedException();
//        }

//        public void Prepend(TKey key, TValue value)
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryPrepend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public void Prepend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryRemove(TKey key, out TValue value)
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryRemoveFirst(out KeyValuePair<TKey, TValue> pair)
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryRemoveLast(out KeyValuePair<TKey, TValue> pair)
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryRemoveMany(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> pair)
//        {
//            throw new NotImplementedException();
//        }

//        public void MarkAppendOnly()
//        {
//            throw new NotImplementedException();
//        }

//        public virtual bool TryAddLast(TKey key, TValue value)
//        {
//            lock (SyncRoot)
//            {
//                if (Last.IsMissing || Comparer.Compare(key, Last.Present.Key) > 0)
//                {
//                    return TryAdd(key, value);
//                }

//                return false;
//            }
//        }

//        public virtual bool TryAddFirst(TKey key, TValue value)
//        {
//            lock (SyncRoot)
//            {
//                if (First.IsMissing || Comparer.Compare(key, First.Present.Key) < 0)
//                {
//                    return TryAdd(key, value);
//                }

//                return false;
//            }
//        }

//        public abstract ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction);

//        public virtual async ValueTask<Opt<TValue>> TryRemove(TKey key)
//        {
//            var result = await TryRemoveMany(key, Lookup.EQ);
//            return result.IsMissing ? Opt<TValue>.Missing : Opt.Present(result.Present.Value);
//        }

//        public virtual ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst()
//        {
//            lock (SyncRoot)
//            {
//                if (First.IsPresent)
//                {
//                    return TryRemoveMany(First.Present.Key, Lookup.LE);
//                }

//                return new ValueTask<Opt<KeyValuePair<TKey, TValue>>>(Opt<KeyValuePair<TKey, TValue>>.Missing);
//            }
//        }

//        public virtual ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast()
//        {
//            lock (SyncRoot)
//            {
//                if (Last.IsPresent)
//                {
//                    return TryRemoveMany(Last.Present.Key, Lookup.GE);
//                }

//                return new ValueTask<Opt<KeyValuePair<TKey, TValue>>>(Opt<KeyValuePair<TKey, TValue>>.Missing);
//            }
//        }

//        public abstract Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction);

//        public abstract ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap);

//        public abstract Task Complete();
//        public bool TryAppend(TKey key, TValue value)
//        {
//            throw new NotImplementedException();
//        }

//        public void Append(TKey key, TValue value)
//        {
//            throw new NotImplementedException();
//        }

//        public bool TryAppend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public void Append<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
//        {
//            throw new NotImplementedException();
//        }

//        public void MarkReadOnly()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}