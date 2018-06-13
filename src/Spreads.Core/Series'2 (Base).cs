// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spreads
{
    /// <summary>
    /// Base class for series implementations.
    /// </summary>
    [CannotApplyEqualityOperator]
    public class BaseSeries
    {
        private static readonly ConditionalWeakTable<BaseSeries, Dictionary<string, object>> Attributes =
            new ConditionalWeakTable<BaseSeries, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic) &&
                dic.TryGetValue(attributeName, out object res))
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

    public abstract class Series<TKey, TValue> : BaseSeries,
        ISpecializedSeries<TKey, TValue, Cursor<TKey, TValue>>
#pragma warning restore 660, 661
    {
        /// <inheritdoc />
        public abstract ICursor<TKey, TValue> GetCursor();

        /// <inheritdoc />
        public abstract KeyComparer<TKey> Comparer { get; }

        /// <inheritdoc />
        public abstract bool IsIndexed { get; }

        /// <inheritdoc />
        public abstract bool IsCompleted { get; }

        public IAsyncEnumerator<KeyValuePair<TKey, TValue>> GetAsyncEnumerator()
        {
            return GetCursor();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return GetCursor();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetCursor();
        }

        Cursor<TKey, TValue> ISpecializedSeries<TKey, TValue, Cursor<TKey, TValue>>.GetCursor()
        {
            return GetWrapper();
        }

        /// <inheritdoc />
        public abstract ValueTask Updated { get; }

        /// <inheritdoc />
        public abstract Opt<KeyValuePair<TKey, TValue>> First { get; }

        /// <inheritdoc />
        public abstract Opt<KeyValuePair<TKey, TValue>> Last { get; }

        /// <inheritdoc />
        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                ThrowHelper.ThrowKeyNotFoundException("Key not found in series");
                return default;
            }
        }

        /// <inheritdoc />
        public abstract bool TryGetValue(TKey key, out TValue value);

        /// <inheritdoc />
        public abstract bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

        /// <inheritdoc />
        public abstract bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp);

        /// <inheritdoc />
        public virtual IEnumerable<TKey> Keys => this.Select(kvp => kvp.Key);

        /// <inheritdoc />
        public virtual IEnumerable<TValue> Values => this.Select(kvp => kvp.Value);

        internal Cursor<TKey, TValue> GetWrapper()
        {
            return new Cursor<TKey, TValue>(GetCursor());
        }

        #region Implicit cast

        /// <summary>
        /// Implicitly convert <see cref="Series{TKey,TValue}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
        /// </summary>
        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(Series<TKey, TValue> series)
        {
            var c = series.GetWrapper();
            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
        }

        #endregion Implicit cast

        #region Unary Operators

        // UNARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
            +(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
            +(TValue constant, Series<TKey, TValue> series)
        {
            // Addition is commutative
            var cursor = new Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>> operator
            -(Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>> operator
            +(Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>> operator
            -(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            -(TValue constant, Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
            *(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
            *(TValue constant, Series<TKey, TValue> series)
        {
            // Multiplication is commutative
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>> operator
            /(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            /(TValue constant, Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>> operator
            %(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            %(TValue constant, Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        // UNARY LOGIC

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            ==(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            ==(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            !=(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            !=(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            >(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            >(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <=(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <=(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator >=(
            Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            >=(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        #endregion Unary Operators

        #region Binary Operators

        // BINARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            +(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            // TODO change to Op2, measure result
            //var op = zipCursor.Apply<AddOp<TValue>>();
            //return op;
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            +(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            +(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            -(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            -(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            -(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            *(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            *(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            *(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            /(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            /(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            /(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            %(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            %(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            %(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        // BINARY LOGIC

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        #endregion Binary Operators
    }

    /// <summary>
    /// Base class for collections (containers).
    /// </summary>
#pragma warning disable 660, 661

    public abstract class ContainerSeries<TKey, TValue, TCursor> : Series<TKey, TValue>,
        ISpecializedSeries<TKey, TValue, TCursor>
#pragma warning restore 660, 661
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private object _syncRoot;

        // ReSharper disable InconsistentNaming
        internal long _version;

        internal long _nextVersion;

        // ReSharper restore InconsistentNaming
        internal bool _isSynchronized = true;

        internal bool _isReadOnly = false;

        internal long Locker;

        //private TaskCompletionSource<bool> _tcs;
        //private TaskCompletionSource<bool> _unusedTcs;

        internal abstract TCursor GetContainerCursor();

        /// <inheritdoc />
        public sealed override bool IsCompleted
        {
            // NB this is set only inside write lock, no other locks are possible
            // after this value is set so we do not need read lock. This is very
            // hot path for MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _isReadOnly; }
        }

        private ConcurrentDictionary<IAsyncStateMachine, object> _cursors = new ConcurrentDictionary<IAsyncStateMachine, object>();

        public override ICursor<TKey, TValue> GetCursor()
        {
            if (IsCompleted)
            {
                return GetContainerCursor();
            }

            var c = new BaseCursorAsync<TKey, TValue, TCursor>(GetContainerCursor());
            _cursors.TryAdd(c, null);
            return c;
        }

        TCursor ISpecializedSeries<TKey, TValue, TCursor>.GetCursor()
        {
            return GetContainerCursor();
        }

        public override bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp)
        {
            if (idx < 0)
            {
                ThrowHelper.ThrowNotImplementedException("TODO Support negative indexes in TryGetAt");
            }
            // TODO (review) not so stupid and potentially throwing impl
            try
            {
                kvp = this.Skip(Math.Max(0, checked((int)(idx)) - 1)).First();
                return true;
            }
            catch
            {
                kvp = default;
                return false;
            }
        }

        #region Synchronization

        /// <summary>
        /// An object for external synchronization.
        /// </summary>
        public object SyncRoot
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        /// <summary>
        /// Takes a write lock, increments _nextVersion field and returns the current value of the _version field.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BeforeWrite()
        {
            // TODO review (recall) why SCM needed access to this

#if DEBUG
            var spinwait = new SpinWait();
#endif
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            var doSpin = _isSynchronized;
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (doSpin)
            {
                if (Interlocked.CompareExchange(ref Locker, 1L, 0L) == 0L)
                {
                    // Interlocked.CompareExchange generated implicit memory barrier
                    // TODO (perf) could do cheaper than interlocked, review
                    // Volatile.Write(ref _nextVersion, _nextVersion + 1L);
                    _nextVersion++;
                    // see the aeron.net 49 & related coreclr issues
                    _nextVersion = Volatile.Read(ref _nextVersion);

                    // Interlocked.Increment(ref _nextVersion);

                    // do not return from a loop, see CoreClr #9692
                    break;
                }
#if DEBUG
                if (spinwait.Count == 10000) // 10 000 is c.700 msec
                {
                    TryUnlock();
                }

#else
                var spinwait = new SpinWait();
#endif
                spinwait.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal virtual void TryUnlock()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Release write lock and increment _version field or decrement _nextVersion field if no updates were made.
        /// Call NotifyUpdate if doVersionIncrement is true
        /// </summary>
        /// <param name="doVersionIncrement"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AfterWrite(bool doVersionIncrement)
        {
            if (!_isSynchronized) return;
            // Volatile.Write will prevent any read/write to move below it
            if (doVersionIncrement)
            {
                // TODO (perf) could do cheaper than interlocked, review
                Volatile.Write(ref _version, _version + 1L);
                // Interlocked.Increment(ref _version);
                NotifyUpdate();
            }
            else
            {
                // set nextVersion back to original version, no changes were made
                // TODO (perf) could do cheaper than interlocked, review
                Volatile.Write(ref _nextVersion, _version);
                // Interlocked.Exchange(ref _nextVersion, _version);
            }

            // release write lock
            // Interlocked.Exchange(ref Locker, 0L);
            // TODO review if this is enough. Iterlocked is right for sure, but is more expensive (slower by more than 25% on field increment test)
            Volatile.Write(ref Locker, 0L);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T ReadLock<T>(Func<T> f)
        {
            T value;
            var doSpin = _isSynchronized;
            SpinWait sw = default;
            do
            {
                var version = doSpin ? Volatile.Read(ref _version) : 0L;
                value = f.Invoke();
                if (!doSpin) { break; }
                var nextVersion = Volatile.Read(ref _nextVersion);
                if (version == nextVersion) { break; }
                sw.SpinOnce();
            } while (true);
            return value;
        }

        // private UpdateSource _updateSource;

        public sealed override ValueTask Updated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                throw new NotSupportedException();

                //if (_updateSource == null)
                //{
                //    BeforeWrite();                          // lock (SyncRoot) {
                //    if (_updateSource == null)
                //    {
                //        _updateSource = new UpdateSource(this);
                //    }
                //    AfterWrite(doVersionIncrement: false);  // } // lock
                //}

                // NB `Version mod short.Max` is used to simulate read-lock (similar to cursor reads)
                // We store LSB of Version as VT token and then compare it to LSB of NextVersion
                // in GetResult and OnCompleted. If versoins ar equal then there were no writes
                // after the task creating and before awating. If versions are not equal then
                // the task completes syncronously. We know that its concumers loop and will
                // call MoveNext to get exact changes (new value or completed). The risk is
                // not false positive but a missed change. In theory, there could be 65k updates
                // after VT creation and before awating. But the chance of this is 1/65k even if
                // we could update data with infinite speed. On benchmarks, VTS machinery could
                // work with at least 1MOPS speed, so we need to update data 65k times during
                // a single (even forgeting that each update triggers notification). Therefore,
                // update speed must be 65k * VTS Mops = 65 Bops (GHz) vs 4.2 GHz of top CPUs.
                // Or VTS should work at 4.2G/65k = 65Kops (both 65 is coincidence, 65^2 = 4225).
                // return _updateSource.GetTask(); // new ValueTask(_updatedSource, unchecked((short)Volatile.Read(ref _version)));
            }
        }

        internal Task DoComplete()
        {
            BeforeWrite();
            if (!_isReadOnly)
            {
                _isReadOnly = true;
                _isSynchronized = false;
                // NB this is API design quirk: AfterWrite checks for _isSynchronized
                // and ignores all further logic if that is false, but BeforeWrite
                // always increments _nextVersion when _isSynchronized = true
                // We have it only here, ok for now but TODO review later
                _nextVersion = _version;
            }
            AfterWrite(false);
            NotifyUpdate();
            return Task.CompletedTask;
        }

        private long Locker2;
        private bool _hasSkipped;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NotifyUpdate()
        {
            if (Locker2 == 1 || Interlocked.CompareExchange(ref Locker2, 1L, 0L) != 0L)
            {
                _hasSkipped = true;
                return;
            }

            // DoNotifyUpdate(null);

#if NETCOREAPP2_1
            ThreadPool.QueueUserWorkItem<object>(DoNotifyUpdate, null, true);
#else
            // This now works but very hacky and fragile, see corefx's 27445 discussion
            // var a = item.Item1;
            // var wcb = Unsafe.As<Action<object>, WaitCallback>(ref a);
            ThreadPool.QueueUserWorkItem(DoNotifyUpdate, null);
#endif

            // _updateSource?.TryNotifyUpdate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoNotifyUpdate(object state)
        {
            foreach (var cursor in _cursors)
            {
                cursor.Key.MoveNext();
            }

            if (_hasSkipped)
            {
                _hasSkipped = false;
                DoNotifyUpdate(null);
            }
            Volatile.Write(ref Locker2, 0);
        }

//        internal class UpdateSource : IValueTaskSource, IAsyncStateMachine
//        {
//            // We do not need AsyncTaskMethodBuilder & state machine when we always signal ourselves
//            // State machine in AsyncEnumerable prototype is needed for _builder.AwaitUnsafeOnCompleted
//            // method to await for inputs (we use it for ReusableWhenAny). Here we only need to properly
//            // handle multiple continuations.

            //            private const int StateStart = -1;

            //            /// <summary>Current state of the state machine.</summary>
            //            private int _state = StateStart;

            //            private AsyncTaskMethodBuilder _builder = AsyncTaskMethodBuilder.Create();

            //            private ConcurrentQueue<(Action<object>, object, object)> _queue;
            //            private ConcurrentQueue<(Action<object>, object, object)> _drainedQueue;

            //            private List<(Action<object>, object, object)> _tempList;

            //            private readonly ContainerSeries<TKey, TValue, TCursor> _series;

            //            private short _version;

            //            public UpdateSource(ContainerSeries<TKey, TValue, TCursor> series)
            //            {
            //                _series = series;
            //            }

            //            public ValueTask GetTask()
            //            {
            //                // Reset();

            //                _version = unchecked((short)Volatile.Read(ref _series._version));

            //                //UpdatedSource inst = this;
            //                //_builder.Start(ref inst); // invokes MoveNext, protected by ExecutionContext guards

            //                switch (GetStatus(unchecked((short)Volatile.Read(ref _series._version))))
            //                {
            //                    case ValueTaskSourceStatus.Succeeded:
            //                        return new ValueTask();

            //                    default:
            //                        return new ValueTask(this, unchecked((short)Volatile.Read(ref _series._version)));
            //                }
            //            }

            //            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //            public ValueTaskSourceStatus GetStatus(short token)
            //            {
            //                if (unchecked((short)Volatile.Read(ref _series._version)) != token || _series._isReadOnly)
            //                {
            //                    // version changed after task creation, we are done
            //                    return ValueTaskSourceStatus.Succeeded;
            //                }
            //                return ValueTaskSourceStatus.Pending;
            //            }

            //            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //            public void GetResult(short token)
            //            {
            //                // TODO false positives are ok, we could just spin in cursors, no need to check something here
            //            }

            //            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //            public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            //            {
            //                if (continuation == null)
            //                {
            //                    ThrowHelper.ThrowArgumentNullException(nameof(continuation));
            //                }

            //                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            //                {
            //                    ThrowHelper.ThrowNotSupportedException();
            //                    // _executionContext = ExecutionContext.Capture();
            //                }

            //                object capturedContext = null;
            //                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            //                {
            //                    SynchronizationContext sc = SynchronizationContext.Current;
            //                    if (sc != null && sc.GetType() != typeof(SynchronizationContext))
            //                    {
            //                        capturedContext = sc;
            //                    }
            //                    else
            //                    {
            //                        TaskScheduler ts = TaskScheduler.Current;
            //                        if (ts != TaskScheduler.Default)
            //                        {
            //                            capturedContext = ts;
            //                        }
            //                    }
            //                }

            //                // Here we have continuation, which means that GetStatus returned Pending
            //                // and the task is being awaited, there is no syncronous path (spin/retry)
            //                // left for the task or cursor to detect version change without awaiting this task.

            //                // If version has changed already (before a writer call UpdateNotify)
            //                // then we could RunContinuation right here with forceAsync = true
            //                // bypassing the queue. Otherwise we add the continuation and context
            //                // to the queue and do additional chack after adding to the queue if
            //                // the writer updates version while we are adding the item to the queue.

            //                if (unchecked((short)Volatile.Read(ref _series._version)) != token || _series._isReadOnly)
            //                {
            //                    // version changed between check for completion via GetResult and await
            //                    RunContinuation((continuation, state, capturedContext), true);
            //                }
            //                else
            //                {
            //                    // atomically create queue if it does not exist yet
            //                    if (_queue == null)
            //                    {
            //                        _series.BeforeWrite(); // lock (SyncRoot) {
            //                        if (_queue == null)
            //                        {
            //                            _queue = new ConcurrentQueue<(Action<object>, object, object)>();
            //                            _drainedQueue = new ConcurrentQueue<(Action<object>, object, object)>();
            //                        }

            //                        _series.AfterWrite(doVersionIncrement: false); // } // lock
            //                    }

            //                    _queue.Enqueue((continuation, state, capturedContext));

            //                    if (unchecked((short)Volatile.Read(ref _series._version)) != token || _series._isReadOnly)
            //                    {
            //                        NotifyUpdate(true);
            //                    }
            //                }
            //            }

            //            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //            internal void TryNotifyUpdate()
            //            {
            //                // True will queue completions on ThreadPool from AfterWrite
            //                // TODO await data producers and continue from them inline
            //                // Currently there is no read loop with ValueTask (working on it), but
            //                // it should be possible to complete awaiter inline.
            //                NotifyUpdate(true);
            //            }

            //            private int _qcdd;

            //            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //            private void NotifyUpdate(bool forceAsync)
            //            {
            //                if (_queue != null && _drainedQueue != null)
            //                {
            //                    // TODO without lock
            //                    lock (_queue)
            //                    {
            //                        var queue = _queue;
            //                        Interlocked.Exchange(ref _queue, _drainedQueue);
            //                        while (queue.TryDequeue(out var item))
            //                        {
            //                            RunContinuation(item, forceAsync);
            //                        }

            //                        _drainedQueue = queue;

            //                        //if (_tempList == null) { _tempList = new List<(Action<object>, object, object)>(); }

            //                        //var qc = _queue.Count;
            //                        //while (_queue.TryDequeue(out var item))
            //                        //{
            //                        //    // RunContinuation(item, forceAsync);
            //                        //    _tempList.Add(item);
            //                        //}

            //                        //if (_tempList.Count != qc)
            //                        //{
            //                        //    _qcdd++;
            //                        //    Console.WriteLine("Queue changed during drain: " + _qcdd);
            //                        //}

            //                        //foreach (var item in _tempList)
            //                        //{
            //                        //    RunContinuation(item, forceAsync);
            //                        //}
            //                        //_tempList.Clear();
            //                    }
            //                }
            //            }

            //            private ConcurrentDictionary<object, object> _debug = new ConcurrentDictionary<object, object>();

            //            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            //            private void RunContinuation((Action<object>, object, object) continuationEx, bool forceAsync)
            //            {
            //                //if (_debug.ContainsKey(continuationEx.Item1))
            //                //{
            //                //    Console.WriteLine("Multiple continuation execution");
            //                //}

            //                var cc = continuationEx.Item3;
            //                if (cc == null)
            //                {
            //                    if (forceAsync)
            //                    {
            //#if NETCOREAPP2_1
            //                        ThreadPool.QueueUserWorkItem(continuationEx.Item1, continuationEx.Item2, true);
            //#else
            //                        // This now works but very hacky and fragile, see corefx's 27445 discussion
            //                        // var a = item.Item1;
            //                        // var wcb = Unsafe.As<Action<object>, WaitCallback>(ref a);
            //                        ThreadPool.QueueUserWorkItem(new WaitCallback(continuationEx.Item1), continuationEx.Item2);
            //#endif
            //                    }
            //                    else
            //                    {
            //                        continuationEx.Item1(continuationEx.Item2);
            //                    }
            //                }
            //                else if (cc is SynchronizationContext sc)
            //                {
            //                    sc.Post(s =>
            //                    {
            //                        continuationEx.Item1(continuationEx.Item2);
            //                    }, null);
            //                }
            //                else if (cc is TaskScheduler ts)
            //                {
            //                    Task.Factory.StartNew(continuationEx.Item1, continuationEx.Item2, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
            //                }

            //                // _debug.TryAdd(continuationEx.Item1, continuationEx.Item1);
            //            }

            //            public void MoveNext()
            //            {
            //                try
            //                {
            //                    switch (_state)
            //                    {
            //                        case StateStart:
            //                            goto case 0;

            //                        case 0:

            //                            try
            //                            {
            //                                if (unchecked((short)Volatile.Read(ref _series._version)) != _version || _series._isReadOnly)
            //                                {
            //                                    NotifyUpdate(false);
            //                                }
            //                                return;
            //                            }
            //                            catch (Exception ex)
            //                            {
            //                                Console.WriteLine(ex);
            //                                throw;
            //                            }

            //                        case 1:
            //                            _state = 0;
            //                            goto case 0;

            //                        default:
            //                            ThrowHelper.ThrowInvalidOperationException();
            //                            return;
            //                    }
            //                }
            //                catch (Exception e)
            //                {
            //                    Console.WriteLine(e);
            //                    return;
            //                }
            //            }

            //            public void SetStateMachine(IAsyncStateMachine stateMachine)
            //            {
            //            }
            //        }

            #endregion Synchronization

            #region Unary Operators

            // UNARY ARITHMETIC

            /// <summary>
            /// Add operator.
            /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
            +(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
            +(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            // Addition is commutative
            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, TCursor>> operator
            -(ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, NegateOp<TValue>, TCursor>(series.GetContainerCursor(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, TCursor>> operator
            +(ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, PlusOp<TValue>, TCursor>(series.GetContainerCursor(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, TCursor>> operator
            -(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>> operator
            -(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor =
                new Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
            *(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
            *(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            // Multiplication is commutative
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, TCursor>> operator
            /(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, DivideOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>> operator
            /(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, TCursor>> operator
            %(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>> operator
            %(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        // UNARY LOGIC

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            ==(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            ==(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            !=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            !=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, LTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                LTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, GTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                GTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, LEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                LEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, GEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                GEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        #endregion Unary Operators

        #region Binary Operators

        // BINARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        //public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
        //    +(ContainerSeries<TKey, TValue, TCursor> series, ContainerSeries<TKey, TValue, TCursor> other)
        //{
        //    var c1 = series.GetContainerCursor();
        //    var c2 = other.GetContainerCursor();
        //    //Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

        //    var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
        //    return zipCursor.Map(AddOp<TValue>.ZipSelector).Source;

        //    //var op2 = new Op2<TKey, TValue, AddOp<TValue>, Zip<TKey, TValue, TValue, TCursor, TCursor>>(zipCursor);

        //    //return op2.Source;
        //}

        #endregion Binary Operators

        #region Implicit cast

        /// <summary>
        /// Implicitly convert <see cref="Series{TKey,TValue}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
        /// </summary>
        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(
            ContainerSeries<TKey, TValue, TCursor> series)
        {
            var c = series.GetWrapper();
            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
        }

        #endregion Implicit cast
    }

    /// <summary>
    /// Base class for collections (containers) with <see cref="ISeries{TKey,TValue}"/> members implemented via a cursor.
    /// </summary>
    public abstract class CursorContainerSeries<TKey, TValue, TCursor> : ContainerSeries<TKey, TValue, TCursor>, IMutableSeries<TKey, TValue>, IDisposable
#pragma warning restore 660, 661
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
#pragma warning restore 660, 661
    {
        private bool _cursorIsSet;
        private TCursor _c;

        private TCursor C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                lock (SyncRoot)
                {
                    if (!_cursorIsSet)
                    {
                        _c = GetContainerCursor();
                        _cursorIsSet = true;
                    }

                    return _c;
                }
            }
        }

        #region ISeries members

        /// <inheritdoc />
        public override Opt<KeyValuePair<TKey, TValue>> First
        {
            get
            {
                lock (SyncRoot)
                {
                    return C.MoveFirst() ? Opt.Present(C.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        /// <inheritdoc />
        public override Opt<KeyValuePair<TKey, TValue>> Last
        {
            get
            {
                lock (SyncRoot)
                {
                    return C.MoveLast() ? Opt.Present(C.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        public override bool TryGetValue(TKey key, out TValue value)
        {
            lock (SyncRoot)
            {
                return C.TryGetValue(key, out value);
            }
        }

        /// <inheritdoc />
        public override bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp)
        {
            // NB call to this.NavCursor.Source.TryGetAt(idx) is recursive (=> SO) and is logically wrong
            if (idx < 0) throw new ArgumentOutOfRangeException(nameof(idx));
            lock (SyncRoot)
            {
                if (!C.MoveFirst())
                {
                    throw new KeyNotFoundException();
                }
                for (var i = 0; i < idx - 1; i++)
                {
                    if (!C.MoveNext())
                    {
                        kvp = default;
                        return false;
                    }
                }
                kvp = C.Current;
                return true;
            }
        }

        /// <inheritdoc />
        public override bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
        {
            lock (SyncRoot)
            {
                if (C.MoveAt(key, direction))
                {
                    kvp = C.Current;
                    return true;
                }

                kvp = default;
                return false;
            }
        }

        /// <inheritdoc />
        public override IEnumerable<TKey> Keys
        {
            get
            {
                lock (SyncRoot)
                {
                    while (C.MoveNext())
                    {
                        yield return C.CurrentKey;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerable<TValue> Values
        {
            get
            {
                lock (SyncRoot)
                {
                    while (C.MoveNext())
                    {
                        yield return C.CurrentValue;
                    }
                }
            }
        }

        #endregion ISeries members

        public virtual void Dispose(bool disposing)
        {
            lock (SyncRoot)
            {
                if (!_cursorIsSet) return;
            }
            _c.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~CursorContainerSeries()
        {
            Dispose(false);
        }

        public abstract long Count { get; }

        public abstract long Version { get; }

        public abstract bool IsAppendOnly { get; }

        public abstract Task<bool> Set(TKey key, TValue value);

        public abstract Task<bool> TryAdd(TKey key, TValue value);

        public virtual Task<bool> TryAddLast(TKey key, TValue value)
        {
            lock (SyncRoot)
            {
                if (Last.IsMissing || Comparer.Compare(key, Last.Present.Key) > 0)
                {
                    return TryAdd(key, value);
                }

                return TaskUtil.FalseTask;
            }
        }

        public virtual Task<bool> TryAddFirst(TKey key, TValue value)
        {
            lock (SyncRoot)
            {
                if (First.IsMissing || Comparer.Compare(key, First.Present.Key) < 0)
                {
                    return TryAdd(key, value);
                }

                return TaskUtil.FalseTask;
            }
        }

        public abstract ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction);

        public virtual async ValueTask<Opt<TValue>> TryRemove(TKey key)
        {
            var result = await TryRemoveMany(key, Lookup.EQ);
            return result.IsMissing ? Opt<TValue>.Missing : Opt.Present(result.Present.Value);
        }

        public virtual ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst()
        {
            lock (SyncRoot)
            {
                if (First.IsPresent)
                {
                    return TryRemoveMany(First.Present.Key, Lookup.LE);
                }

                return new ValueTask<Opt<KeyValuePair<TKey, TValue>>>(Opt<KeyValuePair<TKey, TValue>>.Missing);
            }
        }

        public virtual ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast()
        {
            lock (SyncRoot)
            {
                if (Last.IsPresent)
                {
                    return TryRemoveMany(Last.Present.Key, Lookup.GE);
                }

                return new ValueTask<Opt<KeyValuePair<TKey, TValue>>>(Opt<KeyValuePair<TKey, TValue>>.Missing);
            }
        }

        public abstract Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction);

        public abstract ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap);

        public abstract Task Complete();
    }
}