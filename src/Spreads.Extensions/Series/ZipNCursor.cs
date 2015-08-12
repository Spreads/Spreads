using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Series {
    public class ZipNCursor<K, V, R> : ICursor<K, R> {
        private readonly Func<K, V[], R> _resultSelector;
        private readonly Func<ICursor<K, V>>[] _cursorFactories;

        /// <summary>
        /// All input cursors are continuous
        /// </summary>
        private readonly bool _isContinuous;

        /// <summary>
        /// The largest key of pivots
        /// </summary>
        private K _frontier;
        private int _pivotsAtFrontier;


        public ZipNCursor(Func<K,V[],R> resultSelector, params Func<ICursor<K,V>>[] cursorFactories)
        {
            _resultSelector = resultSelector;
            _cursorFactories = cursorFactories;
        }

        public IComparer<K> Comparer {
            get {
                throw new NotImplementedException();
            }
        }

        public KeyValuePair<K, R> Current {
            get {
                throw new NotImplementedException();
            }
        }

        public IReadOnlyOrderedMap<K, R> CurrentBatch {
            get {
                throw new NotImplementedException();
            }
        }

        public K CurrentKey {
            get {
                throw new NotImplementedException();
            }
        }

        public R CurrentValue {
            get {
                throw new NotImplementedException();
            }
        }

        public bool IsContinuous {
            get {
                throw new NotImplementedException();
            }
        }

        public ISeries<K, R> Source {
            get {
                throw new NotImplementedException();
            }
        }

        object IEnumerator.Current {
            get {
                throw new NotImplementedException();
            }
        }

        public ICursor<K, R> Clone() {
            throw new NotImplementedException();
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public bool MoveAt(K key, Lookup direction) {
            throw new NotImplementedException();
        }

        public bool MoveFirst() {
            throw new NotImplementedException();
        }

        public bool MoveLast() {
            throw new NotImplementedException();
        }

        public bool MoveNext() {
            throw new NotImplementedException();
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        public Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
            throw new NotImplementedException();
        }

        public bool MovePrevious() {
            throw new NotImplementedException();
        }

        public void Reset() {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out R value) {
            throw new NotImplementedException();
        }
    }
}
