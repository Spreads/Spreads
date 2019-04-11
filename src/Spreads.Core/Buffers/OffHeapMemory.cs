// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    public sealed unsafe class OffHeapMemory<T> : RetainableMemory<T>
    {
        private OffHeapBuffer<T> _offHeapBuffer;

        // This object is pooled together with off-heap buffer in LockedObjectPool that
        // acts like a storage and does not drop objects due to data races. The pool
        // reports when an object is not pooled - this could happen if we reached the
        // pool capacity, allocated new OffHeapMemory and then released it.
        // In that case we dispose this object and the counter. In normal pooling
        // case counter remains valid and is in disposed state together with the
        // pooled object.

        // TODO ctor with length, non-pooled case, test dispose/finalize works

        public OffHeapMemory(int minLength) : this(null, minLength)
        { }

        internal OffHeapMemory(RetainableMemoryPool<T> pool, int minLength)
        {
            _poolIdx = pool == null ? (byte)0 : pool.PoolIdx;
            // store counter inside the buffer as proof of concept.
            _isNativeWithHeader = true;
            Init(minLength);
            CounterRef = 0; // after init!
        }

        protected override void Dispose(bool disposing)
        {
            // disposing == false when finilizing and detected that non pooled
            if (disposing)
            {

                var pool = Pool;
                if (pool != null)
                {
                    pool.ReturnInternal(this, clearMemory: false);
                    // pool calls Dispose(false) if a bucket is full
                    return;
                }

                // not pooled, doing finalization work now
                GC.SuppressFinalize(this);
            }

            Debug.Assert(!_isPooled);
            _poolIdx = default;

            AtomicCounter.Dispose(ref CounterRef);
            _isNativeWithHeader = false;
            CounterRef = AtomicCounter.CountMask;

            ClearAfterDispose();

            // Dispose destructs this object and native buffer
            _offHeapBuffer.Dispose();
            // set all to default, increase chances to detect errors
            _offHeapBuffer = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Init(int minimumCapacity)
        {
            _offHeapBuffer.EnsureCapacity(BitUtil.FindNextPositivePowerOfTwo(minimumCapacity) + NativeHeaderSize);
            _pointer = (byte*)_offHeapBuffer._pointer + NativeHeaderSize;
            _length = _offHeapBuffer.Length - NativeHeaderSize;
        }
    }
}
