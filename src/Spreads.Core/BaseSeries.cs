// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads {
    // NB C# is currently used only for AsyncCursor.MNA implementation

    public class BaseSeries {

    }

    public abstract class BaseSeries<TK, TV> : BaseSeries, IReadOnlySeries<TK, TV> {
        private readonly Func<ICursor<TK, TV>> _cursorFactory;
        private ICursor<TK, TV> _c;
        internal int Locker;
        internal TaskCompletionSource<long> UpdateTcs;

        private object _syncRoot;

        protected BaseSeries(Func<ICursor<TK, TV>> cursorFactory = null) {
            _cursorFactory = cursorFactory;
        }

        protected BaseSeries(ISeries<TK, TV> series) : this(series.GetCursor) {
        }

        internal ICursor<TK, TV> C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _c ?? Interlocked.CompareExchange(ref _c, GetCursor(), null); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdateTcs() {
            while (true) {
                var updateTcs = Volatile.Read(ref this.UpdateTcs);
                // stop when the result was already set
                if (updateTcs != null && !updateTcs.TrySetResult(0L)) {
                    continue;
                }
                break;
            }
        }

        public virtual ICursor<TK, TV> GetCursor() {
            if (_cursorFactory != null) {
                return _cursorFactory();
            } else {
                throw new NotImplementedException("Series.GetCursor is not implemented");
            }
        }

        public virtual bool IsIndexed => C.Source.IsIndexed;

        public virtual bool IsReadOnly => C.Source.IsReadOnly;

        public virtual IDisposable Subscribe(IObserver<KeyValuePair<TK, TV>> observer) {
            // TODO not virtual and implement all logic here, including backpressure case
            throw new NotImplementedException();
        }

        IAsyncEnumerator<KeyValuePair<TK, TV>> IAsyncEnumerable<KeyValuePair<TK, TV>>.GetEnumerator() {
            return GetCursor();
        }

        IEnumerator<KeyValuePair<TK, TV>> IEnumerable<KeyValuePair<TK, TV>>.GetEnumerator() {
            return GetCursor();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetCursor();
        }

        public virtual IComparer<TK> Comparer => C.Source.Comparer;

        public object SyncRoot
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _syncRoot ?? Interlocked.CompareExchange(ref _syncRoot, new object(), null);
            }
        }

        ~BaseSeries() {
            _c?.Dispose();
        }

        // IReadOnlySeries members
        public abstract bool IsEmpty { get; }

        public abstract KeyValuePair<TK, TV> First { get; }
        public abstract KeyValuePair<TK, TV> Last { get; }
        public abstract TV this[TK key] { get; }

        public abstract TV GetAt(int idx);

        public abstract IEnumerable<TK> Keys { get; }
        public abstract IEnumerable<TV> Values { get; }

        public abstract bool TryFind(TK key, Lookup direction, out KeyValuePair<TK, TV> value);

        public abstract bool TryGetFirst(out KeyValuePair<TK, TV> value);

        public abstract bool TryGetLast(out KeyValuePair<TK, TV> value);

        public abstract bool TryGetValue(TK key, out TV value);
    }
}
