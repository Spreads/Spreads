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

    public class BaseSeries {
        private static readonly ConditionalWeakTable<BaseSeries, Dictionary<string, object>> Attributes = new ConditionalWeakTable<BaseSeries, Dictionary<string, object>>();

        public object GetAttribute(string attributeName) {
            object res;
            Dictionary<string, object> dic;
            if (Attributes.TryGetValue(this, out dic) && dic.TryGetValue(attributeName, out res)) {
                return res;
            }
            return null;
        }

        public void SetAttribute(string attributeName, object attributeValue) {
            var dic = Attributes.GetOrCreateValue(this);
            dic[attributeName] = attributeValue;
        }
    }

    public abstract class BaseSeries<TK, TV> : BaseSeries, IReadOnlySeries<TK, TV> {
        internal int Locker;
        internal TaskCompletionSource<long> UpdateTcs;

        private object _syncRoot;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdate() {
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
            throw new NotImplementedException("BaseSeries<TK, TV, TCursor>.GetCursor is not implemented");
        }

        public abstract IComparer<TK> Comparer { get; }
        public abstract bool IsIndexed { get; }
        public abstract bool IsReadOnly { get; }

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

        public object SyncRoot {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (_syncRoot == null) {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        // IReadOnlySeries members
        public abstract bool IsEmpty { get; }

        public abstract KeyValuePair<TK, TV> First { get; }
        public abstract KeyValuePair<TK, TV> Last { get; }

        public virtual TV this[TK key] {
            get {
                TV tmp;
                if (TryGetValue(key, out tmp)) {
                    return tmp;
                }
                throw new KeyNotFoundException();
            }
        }

        public abstract TV GetAt(int idx);

        public abstract IEnumerable<TK> Keys { get; }
        public abstract IEnumerable<TV> Values { get; }

        public abstract bool TryFind(TK key, Lookup direction, out KeyValuePair<TK, TV> value);

        public abstract bool TryGetFirst(out KeyValuePair<TK, TV> value);

        public abstract bool TryGetLast(out KeyValuePair<TK, TV> value);

        public abstract bool TryGetValue(TK key, out TV value);
    }
}
