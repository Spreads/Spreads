// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Spreads
{
    public struct ZipW<TKey, TValue, RV, TLC, RC>
        where TLC : ISpecializedCursor<TKey, TValue, TLC>
        where // RC : ISpecializedCursor<TKey, TValue, RC>, ISpecializedCursor<RK, TValue, RC>,
        RC : ISpecializedCursor<TKey, RV, RC>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ZipW<TKey, TValue, RV, TLC, RC>(Series<TKey, TValue, TLC> series)
        {
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int operator
            +(ZipW<TKey, TValue, RV, TLC, RC> series, Series<TKey, RV, RC> other)
        {
            return 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int operator
            +(ZipW<TKey, TValue, RV, TLC, RC> series, ZipW<TKey, TValue, RV, TLC, RC> other)
        {
            return 1;
        }
    }

    /// <summary>
    /// A fast lightweight wrapper around a <see cref="ISpecializedCursor{TKey,TValue,TCursor}"/>
    /// implementing <see cref="ISeries{TKey, TValue}"/> interface using the cursor.
    /// </summary>
#pragma warning disable 660, 661

    // TODO review if we could keep cursor and not initialize every time
    public readonly partial struct Series<TKey, TValue, TCursor> : ISpecializedSeries<TKey, TValue, TCursor>, IAsyncCompleter
#pragma warning restore 660, 661
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // ReSharper disable once InconsistentNaming
        internal readonly TCursor _cursor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Series(TCursor cursor)
        {
            _cursor = cursor;
        }

        /// <summary>
        /// Get an uninitialized cursor that defines this series behavior.
        /// </summary>
        public TCursor CursorDefinition => _cursor;

        /// <summary>
        /// Get strongly-typed enumerator.
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCursor GetEnumerator()
        {
            return _cursor.Initialize();
        }

        /// <summary>
        /// Get strongly-typed async enumerator.
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AsyncCursor<TKey, TValue, TCursor> GetAsyncEnumerator()
        {
            return new AsyncCursor<TKey, TValue, TCursor>(_cursor.Initialize(), true); // TODO review batch mode
        }

        #region ISeries members

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetAsyncEnumerator()
        {
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
            return GetAsyncEnumerator();
#pragma warning restore HAA0401 // Possible allocation of reference type enumerator
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
            return _cursor.Initialize();
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
            return _cursor.Initialize();
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
        }

        IAsyncCursor<TKey, TValue> ISeries<TKey, TValue>.GetAsyncCursor()
        {
            return GetAsyncCursor();
        }

        public AsyncCursor<TKey, TValue, TCursor> GetAsyncCursor()
        {
            return new AsyncCursor<TKey, TValue, TCursor>(GetCursor());
        }

        /// <inheritdoc />

        public KeyComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor.Comparer;
        }

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ICursor<TKey, TValue> ISeries<TKey, TValue>.GetCursor()
        {
            // Async support. ICursorSeries implementations do not implement MNA
            return new AsyncCursor<TKey, TValue, TCursor>(_cursor.Initialize());
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCursor GetCursor()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor.IsIndexed;
        }

        /// <inheritdoc />
        public bool IsCompleted
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor.IsCompleted;
        }

        // TODO (perf) Review if initilize/dispose is too much overhead vs a cached navigation cursor.

        /// <inheritdoc />
        public Opt<KeyValuePair<TKey, TValue>> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                using (var c = _cursor.Initialize())
                {
                    return c.MoveFirst() ? Opt.Present(c.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        /// <inheritdoc />
        public Opt<KeyValuePair<TKey, TValue>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                using (var c = _cursor.Initialize())
                {
                    return c.MoveLast() ? Opt.Present(c.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        public TValue LastValueOrDefault => throw new NotImplementedException();

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            // TODO (!) review, reuse cursor
            using (var c = _cursor.Initialize())
            {
                return c.TryGetValue(key, out value);
            }
        }

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp)
        {
            // NB call to this.NavCursor.Source.TryGetAt(idx) is recursive (=> SO) and is logically wrong
            if (index < 0)
            {
                ThrowHelper.ThrowNotImplementedException("TODO Support negative indexes in TryGetAt");
            }
            using (var c = _cursor.Initialize())
            {
                if (!c.MoveFirst())
                {
                    kvp = default;
                    return false;
                }
                for (var i = 0; i < index - 1; i++)
                {
                    if (!c.MoveNext())
                    {
                        kvp = default;
                        return false;
                    }
                }
                kvp = c.Current;
                return true;
            }
        }

        /// <inheritdoc />
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
        {
            using (var c = _cursor.Initialize())
            {
                if (c.MoveAt(key, direction))
                {
                    kvp = c.Current;
                    return true;
                }

                kvp = default;
                return false;
            }
        }

        /// <inheritdoc />
        public IEnumerable<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                using (var c = _cursor.Initialize())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentKey;
                    }
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                using (var c = _cursor.Initialize())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentValue;
                    }
                }
            }
        }

        /// <inheritdoc />
        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO delete
                //if (typeof(TCursor) == typeof(Segment<TKey, TValue>))
                //{
                //    var sc = (Segment<TKey, TValue>)(object)_cursor;
                //    var c = _cursor;
                //    var sc2 = Unsafe.As<TCursor, Segment<TKey, TValue>>(ref c);
                //    // at least we could specialize on implementation
                //    throw new NotImplementedException();
                //}

                using (var c = _cursor.Initialize())
                {
                    if (c.TryGetValue(key, out var value))
                    {
                        return value;
                    }
                }
                ThrowHelper.ThrowKeyNotFoundException("Key not found");
                return default;
            }
        }

        #endregion ISeries members

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
            where TOp : struct, IOp<TValue> where TCursorLeft : ISpecializedCursor<TKey, TValue, TCursorLeft>
            where TCursorRight : ISpecializedCursor<TKey, TValue, TCursorRight>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable Subscribe(IAsyncCompletable subscriber)
        {
            return _cursor.AsyncCompleter?.Subscribe(subscriber);
        }
    }
}