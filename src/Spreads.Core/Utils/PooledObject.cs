using Spreads.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Utils {

    public abstract class PooledObject<TObject> : IDisposable where TObject : PooledObject<TObject>, new() {
        internal const int DefaultCapacity = 16;
        internal const int MaxCapacity = 128;
        private const int MissLimit = 1000;
        private const double MissShareLimit = 0.5;

        // ReSharper disable StaticMemberInGenericType
        private static BoundedConcurrentQueue<TObject> _pool = new BoundedConcurrentQueue<TObject>(DefaultCapacity);

        private static long _tries;
        private static long _misses;
#if DEBUG
        private static long _falseReturns;
#endif
        protected static readonly object StaticSyncRoot = new object();
        // ReSharper restore StaticMemberInGenericType

        /// <summary>
        /// Initialize disposed object. E.g. rent buffers that were returned to a buffer pool during Dispose.
        /// </summary>
        internal abstract void Init();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TObject Create() {
            _tries++;
            TObject tmp;
            if (_pool.TryDequeue(out tmp)) {
                tmp.Init();
                return tmp;
            }
            _misses++;

            if (_misses > MissLimit && (double)_misses / (double)_tries > MissShareLimit) {
                lock (StaticSyncRoot) {
                    if (_misses > MissLimit && (double)_misses / (double)_tries > MissShareLimit) {
                        PoolCapacity = PoolCapacity + 1;
                    }
                    _tries = 0;
                    _misses = 0;
                }
            }
            var newObj = new TObject();
            newObj.Init();
            return newObj;
        }

        internal static int PoolCapacity
        {
            get { return _pool.Capacity; }
            set
            {
                var newCapacity = Utils.BitUtil.FindNextPositivePowerOfTwo(value);
#if DEBUG
                if (newCapacity >= 1024) {
                    Debug.Fail($"New capacity for {typeof(TObject).Name} is abnormally big: {newCapacity}. Likely objects are not properly disposed.");
                }
#else
                newCapacity = Math.Min(newCapacity, MaxCapacity);
#endif
                if (newCapacity > PoolCapacity) {
                    Trace.WriteLine($"Increasing pool capacity for {typeof(TObject).Name} from {PoolCapacity} to {newCapacity}.");
                } else {
                    Trace.WriteLine($"Tried to increase pool capacity for {typeof(TObject).Name} from {PoolCapacity} to {Utils.BitUtil.FindNextPositivePowerOfTwo(value)}.");
                }
                var newPool = new BoundedConcurrentQueue<TObject>(newCapacity);
                var oldPool = Interlocked.Exchange(ref _pool, newPool);
                TObject tmp;
                while (oldPool.TryDequeue(out tmp)) {
                    // this will return old objects to the new pool
                    tmp.Dispose();
                }
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        public virtual void Dispose(bool disposing) {
            var pooled = _pool.TryEnqueue(this as TObject);
            // TODO review
            if (disposing && !pooled) {
                GC.SuppressFinalize(this);
            }
#if DEBUG
            _falseReturns++;
            if (!pooled && (_falseReturns > MissLimit) && ((double)_falseReturns / (double)_tries) > MissShareLimit) {
                _tries = 0;
                _misses = 0;
                _falseReturns = 0;
                Debug.Fail($"Too many false returns for {typeof(TObject).Name}.");
            }
#endif
        }

        ~PooledObject() {
            Dispose(false);
        }
    }
}