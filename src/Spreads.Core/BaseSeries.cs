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

    public abstract class BaseSeries<TK, TV> : BaseSeries, ISeries<TK, TV> {
        private readonly Func<ICursor<TK, TV>> _cursorFactory;
        private ICursor<TK, TV> _c;
        internal int Locker;
        internal TaskCompletionSource<long> UpdateTcs;

        private object _syncRoot;

        public BaseSeries(Func<ICursor<TK, TV>> cursorFactory = null) {
            _cursorFactory = cursorFactory;
        }

        public BaseSeries(ISeries<TK, TV> series) : this(series.GetCursor) {
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

        public IComparer<TK> Comparer => C.Source.Comparer;

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
    }
}
