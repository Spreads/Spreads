// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads
{
    public readonly partial struct Series<TKey, TValue, TCursor>
    {
        
        #region Unary Operators

        // UNARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
            +(Series<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        ///// <summary>
        ///// Add operator.
        ///// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
            +(TValue constant, Series<TKey, TValue, TCursor> series)
        {
            // Addition is commutative
            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, TCursor>> operator
            -(Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, NegateOp<TValue>, TCursor>(series.GetEnumerator(), default);
            return cursor.Source;
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, TCursor>> operator
            +(Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, PlusOp<TValue>, TCursor>(series.GetEnumerator(), default);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, TCursor>> operator
            -(Series<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>> operator
            -(TValue constant, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
            *(Series<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
            *(TValue constant, Series<TKey, TValue, TCursor> series)
        {
            // Multiplication is commutative
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, TCursor>> operator
            /(Series<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, DivideOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>> operator
            /(TValue constant, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, TCursor>> operator
            %(Series<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>> operator
            %(TValue constant, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>(series.GetEnumerator(), constant);
            return cursor.Source;
        }

        // UNARY LOGIC

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            ==(Series<TKey, TValue, TCursor> series, TValue comparand)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            ==(TValue comparand, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            !=(Series<TKey, TValue, TCursor> series, TValue comparand)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            !=(TValue comparand, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <(Series<TKey, TValue, TCursor> series, TValue comparand)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, LTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <(TValue comparand, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, LTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >(Series<TKey, TValue, TCursor> series, TValue comparand)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, GTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >(TValue comparand, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, GTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <=(Series<TKey, TValue, TCursor> series, TValue comparand)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, LEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <=(TValue comparand, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, LEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator >=(Series<TKey, TValue, TCursor> series, TValue comparand)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, GEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >=(TValue comparand, Series<TKey, TValue, TCursor> series)
        {
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetEnumerator(), comparand, GEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        #endregion Unary Operators

        #region Binary Operators

        // BINARY ARITHMETIC

        // TODO [MethodImpl(MethodImplOptions.AggressiveInlining)] on all

        // save typing & boilerplate. TODO R# Inline this when done
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>
            // ReSharper disable once UnusedParameter.Local
            ZipOp<TOp, TCursorLeft, TCursorRight>(Zip<TKey, TValue, TValue, TCursorLeft, TCursorRight> zipCursor, TOp _)
            where TOp : struct, IOp<TValue> where TCursorLeft : ICursor<TKey, TValue, TCursorLeft>
            where TCursorRight : ICursor<TKey, TValue, TCursorRight>
        {
            return new ZipOp<TKey, TValue, TOp, TCursorLeft, TCursorRight>(zipCursor);
        }

        // BINARY LOGIC

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
            ==(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, TCursor> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue, TCursor> series, Series<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetCursor();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>>> operator
            ==(Series<TKey, TValue> series, Series<TKey, TValue, TCursor> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetCursor();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
            !=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, TCursor> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetCursor();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>>> operator
            !=(Series<TKey, TValue> series, Series<TKey, TValue, TCursor> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetCursor();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
            <=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, TCursor> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetCursor();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>>> operator
            <=(Series<TKey, TValue> series, Series<TKey, TValue, TCursor> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetCursor();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
            >=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, TCursor> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue, TCursor> series, Series<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetCursor();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>>> operator
            >=(Series<TKey, TValue> series, Series<TKey, TValue, TCursor> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetCursor();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
            <(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, TCursor> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue, TCursor> series, Series<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetCursor();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>>> operator
            <(Series<TKey, TValue> series, Series<TKey, TValue, TCursor> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetCursor();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
            >(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, TCursor> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue, TCursor> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue, TCursor> series, Series<TKey, TValue> other)
        {
            if (ReferenceEquals(other, null)) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetCursor();

            var zipCursor = new Zip<TKey, TValue, TValue, TCursor, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool, Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>>> operator
            >(Series<TKey, TValue> series, Series<TKey, TValue, TCursor> other)
        {
            if (ReferenceEquals(series, null)) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetCursor();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, TCursor>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        #endregion Binary Operators

        #region Implicit cast

        /// <summary>
        /// Implicitly convert specialized <see cref="Series{TKey,TValue,TCursor}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
        /// with <see cref="Cursor{TKey,TValue}"/> as <typeparamref name="TCursor"/>.
        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
        /// </summary>
        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(Series<TKey, TValue, TCursor> series)
        //        {
        //#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
        //            var c = new Cursor<TKey, TValue>(series._cursor);
        //#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
        //            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
        //        }

        /// <summary>
        /// Erase cursor type <typeparamref name="TCursor"/> to <see cref="Cursor{TKey,TValue}"/>.
        /// </summary>
        public Series<TKey, TValue, Cursor<TKey, TValue>> Unspecialized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => throw new NotImplementedException();// this;
        }

        // TODO
        //public Series<TKey, TValue, Cursor<TKey, TValue>> _
        //{
        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    get => throw new NotImplementedException();// this;
        //}

        #endregion Implicit cast

    }
}