// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads
{
    public struct ZipW<TKey, TValue, RV, TLC, RC>
        where TLC : ICursor<TKey, TValue, TLC>
        where // RC : ISpecializedCursor<TKey, TValue, RC>, ISpecializedCursor<RK, TValue, RC>,
        RC : ICursor<TKey, RV, RC>
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
    /// A fast lightweight wrapper around a <see cref="ICursor{TKey,TValue,TCursor}"/>
    /// implementing <see cref="ISeries{TKey, TValue}"/> interface using the cursor.
    /// </summary>
#pragma warning disable 660, 661

    // TODO review if we could keep cursor and not initialize every time
    public readonly partial struct Series<TKey, TValue, TCursor> : ISeries<TKey, TValue, TCursor>, IAsyncCompleter
#pragma warning restore 660, 661
        where TCursor : ICursor<TKey, TValue, TCursor>
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
        public AsyncCursor<TKey, TValue, TCursor> GetAsyncEnumerator(CancellationToken ct = default)
        {
            return new AsyncCursor<TKey, TValue, TCursor>(_cursor.Initialize(), true); // TODO review batch mode
        }

        #region ISeries members

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetAsyncEnumerator(CancellationToken ct = default)
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

        //IAsyncCursor<TKey, TValue> ISeries<TKey, TValue>.GetAsyncCursor()
        //{
        //    return GetAsyncCursor();
        //}

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable Subscribe(IAsyncCompletable subscriber)
        {
            return _cursor.AsyncCompleter?.Subscribe(subscriber);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // TODO review if we must check cursor state before that or all cursors
            // could be disposed from any state withour exception
            _cursor.Dispose();
        }

        public ContainerLayout ContainerLayout => throw new NotImplementedException();

        public Mutability Mutability => throw new NotImplementedException();

        public KeySorting KeySorting => throw new NotImplementedException();

        public ulong? RowCount => throw new NotImplementedException();

        public bool IsEmpty => throw new NotImplementedException();

        System.Collections.Generic.IAsyncEnumerator<KeyValuePair<TKey, TValue>> System.Collections.Generic.IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return GetAsyncEnumerator(cancellationToken);
        }
    }
}