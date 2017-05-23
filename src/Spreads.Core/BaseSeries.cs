// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Cursors;
using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads
{
    /// <summary>
    /// Base class for series implementations.
    /// </summary>
    [CannotApplyEqualityOperator]
    public class BaseSeries
    {
        private static readonly ConditionalWeakTable<BaseSeries, Dictionary<string, object>> Attributes = new ConditionalWeakTable<BaseSeries, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic) && dic.TryGetValue(attributeName, out object res))
            {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Set any custom attribute to a series. An attribute is available during lifetime of a series and is available via GetAttribute() method.
        /// </summary>
        public void SetAttribute(string attributeName, object attributeValue)
        {
            var dic = Attributes.GetOrCreateValue(this);
            dic[attributeName] = attributeValue;
        }
    }

    /// <summary>
    /// Base generic class for all series implementations.
    /// </summary>
    /// <typeparam name="TKey">Type of series keys.</typeparam>
    /// <typeparam name="TValue">Type of series values.</typeparam>
#pragma warning disable 660, 661

    public abstract class BaseSeries<TKey, TValue> : BaseSeries, IReadOnlySeries<TKey, TValue>
#pragma warning restore 660,661
    {
        /// <inheritdoc />
        public abstract ICursor<TKey, TValue> GetCursor();

        /// <inheritdoc />
        public abstract KeyComparer<TKey> Comparer { get; }

        /// <inheritdoc />
        public abstract bool IsIndexed { get; }

        /// <inheritdoc />
        public abstract bool IsReadOnly { get; }

        /// <inheritdoc />
        public virtual IDisposable Subscribe(IObserver<KeyValuePair<TKey, TValue>> observer)
        {
            // TODO not virtual and implement all logic here, including backpressure case
            throw new NotImplementedException();
        }

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return GetCursor();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return GetCursor();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetCursor();
        }

        /// <inheritdoc />
        public abstract Task<bool> Updated { get; }

        /// <inheritdoc />
        public abstract bool IsEmpty { get; }

        /// <inheritdoc />
        public abstract KeyValuePair<TKey, TValue> First { get; }

        /// <inheritdoc />
        public abstract KeyValuePair<TKey, TValue> Last { get; }

        /// <inheritdoc />
        public virtual TValue this[TKey key]
        {
            get
            {
                if (TryFind(key, Lookup.EQ, out var tmp))
                {
                    return tmp.Value;
                }
                throw new KeyNotFoundException();
            }
        }

        /// <inheritdoc />
        public abstract TValue GetAt(int idx);

        /// <inheritdoc />
        public abstract IEnumerable<TKey> Keys { get; }

        /// <inheritdoc />
        public abstract IEnumerable<TValue> Values { get; }

        /// <inheritdoc />
        public abstract bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value);

        /// <inheritdoc />
        public abstract bool TryGetFirst(out KeyValuePair<TKey, TValue> value);

        /// <inheritdoc />
        public abstract bool TryGetLast(out KeyValuePair<TKey, TValue> value);

        internal Cursor<TKey, TValue> GetWrapper()
        {
            return new Cursor<TKey, TValue>(GetCursor());
        }

        #region Implicit cast

        /// <summary>
        /// Implicitly convert <see cref="BaseSeries{TKey,TValue}"/> to <see cref="CursorSeries{TKey,TValue,TCursor}"/>
        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
        /// </summary>
        public static implicit operator CursorSeries<TKey, TValue, Cursor<TKey, TValue>>(BaseSeries<TKey, TValue> series)
        {
            var c = series.GetWrapper();
            return new CursorSeries<TKey, TValue, Cursor<TKey, TValue>>(c);
        }

        #endregion Implicit cast

        #region Unary Operators

        // UNARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
            +(BaseSeries<TKey, TValue> series, TValue constant)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
            +(TValue constant, BaseSeries<TKey, TValue> series)
        {
            // Addition is commutative
            var cursor = new ArithmeticCursor<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>> operator
            -(BaseSeries<TKey, TValue> series)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>> operator
            +(BaseSeries<TKey, TValue> series)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>> operator
            -(BaseSeries<TKey, TValue> series, TValue constant)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            -(TValue constant, BaseSeries<TKey, TValue> series)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
            *(BaseSeries<TKey, TValue> series, TValue constant)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
            *(TValue constant, BaseSeries<TKey, TValue> series)
        {
            // Multiplication is commutative
            var cursor = new ArithmeticCursor<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>> operator
            /(BaseSeries<TKey, TValue> series, TValue constant)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            /(TValue constant, BaseSeries<TKey, TValue> series)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>> operator
            %(BaseSeries<TKey, TValue> series, TValue constant)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            %(TValue constant, BaseSeries<TKey, TValue> series)
        {
            var cursor = new ArithmeticCursor<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        // UNARY LOGIC

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            ==(BaseSeries<TKey, TValue> series, TValue comparand)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            ==(TValue comparand, BaseSeries<TKey, TValue> series)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            !=(BaseSeries<TKey, TValue> series, TValue comparand)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            !=(TValue comparand, BaseSeries<TKey, TValue> series)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            <(BaseSeries<TKey, TValue> series, TValue comparand)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, LTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            <(TValue comparand, BaseSeries<TKey, TValue> series)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, LTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            >(BaseSeries<TKey, TValue> series, TValue comparand)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, GTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            >(TValue comparand, BaseSeries<TKey, TValue> series)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, GTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            <=(BaseSeries<TKey, TValue> series, TValue comparand)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, LEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            <=(TValue comparand, BaseSeries<TKey, TValue> series)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, LEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator >=(BaseSeries<TKey, TValue> series, TValue comparand)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, GEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>> operator
            >=(TValue comparand, BaseSeries<TKey, TValue> series)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var cursor = new ComparisonCursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand, GEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        #endregion Unary Operators

        #region Binary Operators

        // BINARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            +(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            +(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            +(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, TValue, TValue, TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            -(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            -(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            -(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, TValue, TValue, TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            *(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            *(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            *(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, TValue, TValue, TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            /(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            /(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            /(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, TValue, TValue, TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            %(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            %(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, TValue, TValue, TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            %(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, TValue, TValue, TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new ZipCursor<TKey, TValue, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, selector);
            return zipCursor.Source;
        }

        // BINARY LOGIC

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            ==(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, EQOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            ==(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, EQOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            ==(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, EQOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            !=(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, NEQOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            !=(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, NEQOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            !=(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, NEQOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            <=(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, LEOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            <=(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, LEOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            <=(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, LEOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            >=(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, GEOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            >=(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, GEOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            >=(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, GEOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            <(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, LTOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            <(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, LTOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            <(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, LTOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            >(BaseSeries<TKey, TValue> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, GTOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            >(CursorSeries<TKey, TValue, Cursor<TKey, TValue>> series, BaseSeries<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, GTOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static CursorSeries<TKey, bool, ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>> operator
            >(BaseSeries<TKey, TValue> series, CursorSeries<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new ZipCursor<TKey, TValue, TValue, bool, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2, GTOp<TValue>.ZipSelector);
            return zipCursor.Source;
        }

        #endregion Binary Operators
    }

    /// <summary>
    /// Base class for collections (containers).
    /// </summary>
    public abstract class ContainerSeries<TKey, TValue> : BaseSeries<TKey, TValue>
    {
        internal long _version;

        internal long _nextVersion;
        private int _writeLocker;
        private TaskCompletionSource<bool> _tcs;
        private TaskCompletionSource<bool> _unusedTcs;

        /// <summary>
        /// Takes a write lock, increments _nextVersion field and returns the current value of the _version field.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected long BeforeWrite(bool takeLock = true)
        {
            var spinwait = new SpinWait();
            long version = -1L;
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (takeLock)
            {
                if (Interlocked.CompareExchange(ref _writeLocker, 1, 0) == 0)
                {
                    // Interlocked.CompareExchange generated implicit memory barrier
                    var nextVersion = _nextVersion + 1L;
                    // Volatile.Write prevents the read above to move below
                    Volatile.Write(ref _nextVersion, nextVersion);
                    // Volatile.Read prevents any read/write after to to move above it
                    // see CoreClr 6121, esp. comment by CarolEidt
                    version = Volatile.Read(ref _version);
                    // do not return from a loop, see CoreClr #9692
                    break;
                }
                if (spinwait.Count == 10000) // 10 000 is c.700 msec
                {
                    TryUnlock();
                }
                // NB Spinwait significantly increases performance probably due to PAUSE instruction
                spinwait.SpinOnce();
            }
            return version;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal virtual void TryUnlock()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Release write lock and increment _version field or decrement _nextVersion field if no updates were made
        /// </summary>
        /// <param name="version"></param>
        /// <param name="doIncrement"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AfterWrite(long version, bool doIncrement = true)
        {
            if (version < 0L) return;

            // Volatile.Write will prevent any read/write to move below it
            if (doIncrement)
            {
                Volatile.Write(ref _version, version + 1L);

                // NB no Interlocked inside write lock, readers must follow pattern to retry MN after getting `Updated` Task, and MN has it's own read lock. For other cases the behavior in undefined.
                var tcs = _tcs;
                _tcs = null;
                tcs?.SetResult(true);
            }
            else
            {
                // set nextVersion back to original version, no changes were made
                Volatile.Write(ref _nextVersion, version);
            }
            // release write lock
            //Interlocked.Exchange(ref _writeLocker, 0);
            // TODO review if this is enough. Iterlocked is right for sure, but is more expensive (slower by more than 25% on field increment test)
            Volatile.Write(ref _writeLocker, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdate(bool result = true)
        {
            // NB in some cases (inside write lock) interlocked is not needed, but we then use this same lgic manually without Interlocked
            var tcs = Interlocked.Exchange(ref _tcs, null);
            tcs?.SetResult(result);
        }

        /// <summary>
        /// A Task that is completed with True whenever underlying data is changed.
        /// Internally used for signaling to async cursors.
        /// After getting the Task one should check if any changes happened (version change or cursor move) before awating the task.
        /// If the task is completed with false then the series is read-only, immutable or complete.
        /// </summary>
        public override Task<bool> Updated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // saving one allocation vs Interlocked.Exchange call
                var unusedTcs = Interlocked.Exchange(ref _unusedTcs, null);
                var newTcs = unusedTcs ?? new TaskCompletionSource<bool>();
                var tcs = Interlocked.CompareExchange(ref _tcs, newTcs, null);
                if (tcs == null)
                {
                    // newTcs was put to the _tcs field, use it
                    tcs = newTcs;
                }
                else
                {
                    Volatile.Write(ref _unusedTcs, newTcs);
                }
                return tcs.Task;
            }
        }
    }
}