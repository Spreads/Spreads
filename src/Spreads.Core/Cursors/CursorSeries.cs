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

namespace Spreads.Cursors
{
    /// <summary>
    /// Base abstract class for cursor series (objects that implement both IReadOnlySeries and ICursor).
    /// </summary>
    internal abstract class CursorSeries<TKey, TValue, TCursor> : BaseSeries<TKey, TValue>, ICursor<TKey, TValue>
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
                    _navigationCursor = Clone();

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

        public IReadOnlySeries<TKey, TValue> Source => this;

        public TCursor GetEnumerator()
        {
            return Clone();
        }

        public override ICursor<TKey, TValue> GetCursor()
        {
            return new BaseCursorAsync<TKey, TValue, TCursor>(Clone);
        }

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

        public override IEnumerable<TKey> Keys
        {
            get
            {
                using (var c = this.GetCursor())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentKey;
                    }
                }
            }
        }

        public override IEnumerable<TValue> Values
        {
            get
            {
                using (var c = this.GetCursor())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentValue;
                    }
                }
            }
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken) => throw new NotSupportedException("Async MoveNext should use BaseCursor via CursorSeries");

        object IEnumerator.Current => this.Current;

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        public abstract KeyValuePair<TKey, TValue> Current { get; }

        public abstract TKey CurrentKey { get; }
        public abstract TValue CurrentValue { get; }

        public abstract TCursor Clone();

        public abstract IReadOnlySeries<TKey, TValue> CurrentBatch { get; }

        public abstract void Dispose();

        public abstract bool IsContinuous { get; }

        public abstract bool MoveAt(TKey key, Lookup direction);

        public abstract bool MoveFirst();

        public abstract bool MoveLast();

        public abstract bool MoveNext();

        public abstract Task<bool> MoveNextBatch(CancellationToken cancellationToken);

        public abstract bool MovePrevious();

        public abstract void Reset();
    }
}