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
    /// <summary>
    /// Base abstract class for cursor series (objects that implement both IReadOnlySeries and ICursor).
    /// </summary>
    public abstract class CursorSeries<TKey, TValue, TCursor> : BaseSeries<TKey, TValue>, ICursor<TKey, TValue>
        where TCursor : CursorSeries<TKey, TValue, TCursor>
    {
        private TCursor _navigationCursor;
        internal int ThreadId = Environment.CurrentManagedThreadId;
        internal CursorState State;

        internal TCursor NavCursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_navigationCursor == null)
                {
                    var initialState = State;
                    _navigationCursor = Create();

                    // this.Clone() will return `this` as 'TCursor if requested from the same thread and the state was 0
                    // but it must set state to 1 after that
                    if (initialState == CursorState.None
                        && ThreadId == Environment.CurrentManagedThreadId
                        && State != CursorState.Initialized)
                    {
                        Trace.TraceWarning(
                            "CursorSeries.Clone should return itself when state was zero and the method was called from the owner thread.");
#if DEBUG
                        // Enforce this while in DEBUG, but it is actuallu `should` rather than `must`
                        throw new ApplicationException("CursorSeries.Clone must return itself when state was zero and the method was called from the owner thread.");
#endif
                    }

                    _navigationCursor.State = CursorState.Navigating;
                }
                return _navigationCursor;
            }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> Source => this;

        /// <inheritdoc />
        public override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var clone = Create();
            return clone;
        }

        /// <inheritdoc />
        public override ICursor<TKey, TValue> GetCursor()
        {
            return new BaseCursorAsync<TKey, TValue, TCursor>(Create);
        }

        /// <inheritdoc />
        public override bool IsEmpty
        {
            get
            {
                lock (SyncRoot)
                {
                    return !NavCursor.MoveFirst();
                }
            }
        }

        /// <inheritdoc />
        public override KeyValuePair<TKey, TValue> First
        {
            get
            {
                lock (SyncRoot)
                {
                    var c = NavCursor;
                    return c.MoveFirst() ? c.Current : throw new InvalidOperationException("Series is empty");
                }
            }
        }

        /// <inheritdoc />
        public override KeyValuePair<TKey, TValue> Last
        {
            get
            {
                lock (SyncRoot)
                {
                    var c = NavCursor;
                    return c.MoveLast() ? c.Current : throw new InvalidOperationException("Series is empty");
                }
            }
        }

        /// <inheritdoc />
        public override bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value)
        {
            lock (SyncRoot)
            {
                var c = NavCursor;
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
        public override bool TryGetFirst(out KeyValuePair<TKey, TValue> value)
        {
            lock (SyncRoot)
            {
                var c = NavCursor;
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
        public override bool TryGetValue(TKey key, out TValue value)
        {
            lock (SyncRoot)
            {
                var c = NavCursor;
                if (c.IsContinuous)
                {
                    return c.TryGetValue(key, out value);
                }
                if (c.MoveAt(key, Lookup.EQ))
                {
                    value = c.CurrentValue;
                    return true;
                }
                value = default(TValue);
                return false;
            }
        }

        /// <inheritdoc />
        public override bool TryGetLast(out KeyValuePair<TKey, TValue> value)
        {
            lock (SyncRoot)
            {
                var c = NavCursor;
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
        public override IEnumerable<TKey> Keys
        {
            get
            {
                using (var c = GetCursor())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentKey;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerable<TValue> Values
        {
            get
            {
                using (var c = GetCursor())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentValue;
                    }
                }
            }
        }

        /// <summary>
        /// Create a copy of TCursor initialized to its position.
        /// </summary>
        public TCursor Clone()
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
        public Task<bool> MoveNext(CancellationToken cancellationToken) => throw new NotSupportedException("Async MoveNext should use BaseCursor via CursorSeries");

        object IEnumerator.Current => Current;

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current => new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue);

        /// <inheritdoc />
        public abstract TKey CurrentKey { get; }

        /// <inheritdoc />
        public abstract TValue CurrentValue { get; }

        /// <summary>
        /// Create an uninitialized copy of TCursor
        /// </summary>
        public abstract TCursor Create();

        /// <inheritdoc />
        public abstract IReadOnlySeries<TKey, TValue> CurrentBatch { get; }

        /// <inheritdoc />
        public abstract void Dispose();

        /// <inheritdoc />
        public abstract bool IsContinuous { get; }

        /// <inheritdoc />
        public abstract bool MoveAt(TKey key, Lookup direction);

        /// <inheritdoc />
        public abstract bool MoveFirst();

        /// <inheritdoc />
        public abstract bool MoveLast();

        /// <inheritdoc />
        public abstract bool MoveNext();

        /// <inheritdoc />
        public abstract Task<bool> MoveNextBatch(CancellationToken cancellationToken);

        /// <inheritdoc />
        public abstract bool MovePrevious();

        /// <inheritdoc />
        public abstract void Reset();
    }

    //internal static class CursorSeriesExtensions
    //{
    //    // TODO think how to use this approach for chaining extensions and keep type info
    //    // We have 4 collections (SM, SCM, DM, IM) and everything else should be CursorSeries
    //    // Could make CursorSeries public - type info will be also helpful for debugging
    //    internal static void Map<T, TKey, TValue, TCursor>(this T value)
    //        where T : CursorSeries<TKey, TValue, TCursor>
    //        where TCursor : CursorSeries<TKey, TValue, TCursor>
    //    {
    //        var c = value.Clone();
    //    }
    //}
}