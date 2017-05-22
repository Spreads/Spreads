// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Cursors
{
    // TODO (docs) The goal is to have only containers as classes. And even this is questionable e.g. for
    // SMs inside SCM. We need classes for locking and finalization (currently), but should try to remove finalization
    // Instead, SCM should properly dispose its inner chunks, which could be made stucts. Disposal is needed to
    // return buffers. But buffers could be made finalizable or just GCed when not disposed (buffer pools will allocate new ones)
    // Locking could be done via a third buffer which could be an unsafe memory.

    /// <summary>
    /// A lightweight wrapper around a <see cref="ICursorSeries{TKey, TValue, TCursor}"/>
    /// implementing <see cref="IReadOnlySeries{TKey, TValue}"/> interface using the cursor.
    /// </summary>
    public struct CursorSeries<TKey, TValue, TCursor> : IReadOnlySeries<TKey, TValue>
        where TCursor : ICursorSeries<TKey, TValue, TCursor>
    {
        internal readonly TCursor _cursor;

        internal CursorSeries(TCursor cursor)
        {
            _cursor = cursor;
        }

        /// <summary>
        /// Get strongly-typed enumerator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TCursor GetEnumerator()
        {
            return _cursor.Initialize();
        }

        #region ISeries members

        IDisposable IObservable<KeyValuePair<TKey, TValue>>.Subscribe(IObserver<KeyValuePair<TKey, TValue>> observer)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        /// <inheritdoc />
        public ICursor<TKey, TValue> GetCursor()
        {
            // Async support. ICursorSeries implementations do not implement MNA
            return new BaseCursorAsync<TKey, TValue, TCursor>(_cursor.Initialize());
        }

        /// <inheritdoc />
        public bool IsIndexed => _cursor.IsIndexed;

        /// <inheritdoc />
        public bool IsReadOnly
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.IsReadOnly; }
        }

        /// <inheritdoc />
        public Task<bool> Updated
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Updated; }
        }

        #endregion ISeries members

        #region IReadOnlySeries members

        /// <inheritdoc />
        public bool IsEmpty
        {
            get
            {
                using (var c = _cursor.Initialize())
                {
                    return !c.MoveFirst();
                }
            }
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> First
        {
            get
            {
                using (var c = _cursor.Initialize())
                {
                    return c.MoveFirst() ? c.Current : throw new InvalidOperationException("A series is empty.");
                }
            }
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Last
        {
            get
            {
                using (var c = _cursor.Initialize())
                {
                    return c.MoveLast() ? c.Current : throw new InvalidOperationException("A series is empty.");
                }
            }
        }

        /// <inheritdoc />
        public TValue GetAt(int idx)
        {
            // NB call to this.NavCursor.Source.GetAt(idx) is recursive (=> SO) and is logically wrong
            if (idx < 0) throw new ArgumentOutOfRangeException(nameof(idx));
            using (var c = _cursor.Initialize())
            {
                if (!c.MoveFirst())
                {
                    throw new KeyNotFoundException();
                }
                for (int i = 0; i < idx - 1; i++)
                {
                    if (!c.MoveNext())
                    {
                        throw new KeyNotFoundException();
                    }
                }
                return c.CurrentValue;
            }
        }

        /// <inheritdoc />
        public bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value)
        {
            using (var c = _cursor.Initialize())
            {
                if (c.MoveAt(key, direction))
                {
                    value = c.Current;
                    return true;
                }
                value = default(KeyValuePair<TKey, TValue>);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetFirst(out KeyValuePair<TKey, TValue> value)
        {
            using (var c = _cursor.Initialize())
            {
                if (c.MoveFirst())
                {
                    value = c.Current;
                    return true;
                }
                value = default(KeyValuePair<TKey, TValue>);
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetLast(out KeyValuePair<TKey, TValue> value)
        {
            using (var c = _cursor.Initialize())
            {
                if (c.MoveLast())
                {
                    value = c.Current;
                    return true;
                }
                value = default(KeyValuePair<TKey, TValue>);
                return false;
            }
        }

        /// <inheritdoc />
        public IEnumerable<TKey> Keys
        {
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
            get
            {
                if (TryFind(key, Lookup.EQ, out var tmp))
                {
                    return tmp.Value;
                }
                Collections.Generic.ThrowHelper.ThrowKeyNotFoundException();
                return default(TValue);
            }
        }

        #endregion IReadOnlySeries members

        #region Operators

        // TODO this is a sample how to bridge BaseSeries and CursorSeries. This is only relevant for operators.
        // Extensions work either on interfaces or directly on specialized types.

        /// <summary>
        /// Add operator.
        /// </summary>
        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, AddOp<TValue>, SpecializedWrapper<TKey, TValue>>> operator +(CursorSeries<TKey, TValue, TCursor> series, BaseSeries<TKey, TValue> other)
        {
            throw new NotImplementedException();
        }

        public static CursorSeries<TKey, TValue, ArithmeticCursor<TKey, TValue, AddOp<TValue>, SpecializedWrapper<TKey, TValue>>> operator +(BaseSeries<TKey, TValue> other, CursorSeries<TKey, TValue, TCursor> series)
        {
            throw new NotImplementedException();
        }

        #endregion Operators
    }
}