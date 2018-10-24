// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Runtime.CompilerServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    public sealed unsafe class OffHeapMemory<T> : RetainableMemory<T> //where T : struct
    {
        private OffHeapBuffer<T> _offHeapBuffer;

        internal RetainableMemoryPool<T, OffHeapMemory<T>> _pool;

        // This object is pooled together with off-heap buffer in LockedObjectPool that
        // act like a storage and does not drop objects due to data races. The pool
        // reports when an object is not pooled - this could happen if we reached the
        // pool capacity, allocated new OffHeapMemory and then released it.
        // In that case we dispose this object and the counter. In normal pooling
        // case counter remains valid and is in disposed state together with the
        // pooled object.

        // TODO ctor with lengtgh, non-pooled case, test dispose/finalize works

        public OffHeapMemory(int minLength) : this(null, minLength)
        { }

        internal OffHeapMemory(RetainableMemoryPool<T, OffHeapMemory<T>> pool, int minLength) :
            // NOTE: this object must release _counter when finalized or not pooled
            // Base class calls _counter.Dispose in it's dispose method and this is enough
            // for shared-memory backed counters. Base class doesn't know about ACS.
            base(AtomicCounterService.AcquireCounter())
        {
            _pool = pool;
            Init(minLength);
        }

        protected override void Dispose(bool disposing)
        {
            var count = Counter.IsValid ? Counter.Count : -1;
            if (count != 0)
            {
                if (count > 0) { ThrowDisposingRetained<OffHeapMemory<T>>(); }
                else { ThrowDisposed<OffHeapMemory<T>>(); }
            }

            // disposing == false when finilizing and detected that non pooled
            if (disposing)
            {
                if (IsPooled)
                {
                    ThrowAlreadyPooled<OffHeapMemory<T>>();
                }

                _pool?.ReturnNoChecks(this, clearArray:false); // this is off-heap, we do not need to clean blittables

                if (!IsPooled)
                {
                    // detach from pool
                    _pool = null;
                    // as if finalizing
                    GC.SuppressFinalize(this);
                    Dispose(false);
                }
            }
            else
            {
                Counter.Dispose();
                AtomicCounterService.ReleaseCounter(Counter);
                Counter = default;
                ClearOnDispose();

                // Dispose destructs this object and native buffer
                _offHeapBuffer.Dispose();
                // set all to default, increase chances to detect errors
                _offHeapBuffer = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Init(int minimumCapacity)
        {
            _offHeapBuffer.EnsureCapacity(BitUtil.FindNextPositivePowerOfTwo(minimumCapacity));
            _pointer = _offHeapBuffer._pointer;
            _length = _offHeapBuffer.Length;
            _offset = 0;
        }
    }
}
