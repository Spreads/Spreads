// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    public unsafe class OffHeapMemory<T> : RetainableMemory<T> where T : struct
    {
        private OffHeapBuffer<T> _offHeapBuffer;

        // TODO pool == null could be used as _pooled
        internal OffHeapBufferPool<T> _pool;

        // This object is pooled together with off-heap buffer in LockedObjectPool that
        // act like a storage and does not drop objects due to data races. The pool
        // reports when an object is not pooled - this could happen if we reached the
        // pool capacity, allocated new OffHeapMemory and then released it.
        // In that case we dispose this object and the counter. In normal pooling
        // case counter remains valid and is in disposed state together with the
        // pooled object.
        // TODO Renewing counter is unsafe manual operation with its pointer
        // and is not exposed as an API so far. AC service is implementation detail
        // of RetainableMemory child implementations, it could be a shared memory as well.

        // TODO ctor with lengtgh, non-pooled case, test dispose/finalize works

        public OffHeapMemory(int minLength) : this(null)
        {
            // Init must work on default struct of OffHeapBuffer
            Init(minLength);
        }

        internal OffHeapMemory(OffHeapBufferPool<T> pool) :
            // NOTE: this object must release _counter when finalized or not pooled
            // Base class calls _counter.Dispose in it's dispose method and this is enough
            // for shared-memory backed counters. Base class doesn't know about ACS.
            base(AtomicCounterService.AcquireCounter())
        {
            _pool = pool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // hope for devirt
        protected override void OnNoReferences()
        {
            Dispose(true);
        }

        //internal bool IsPooled
        //{
        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    get => _pool ;
        //}

        protected override void Dispose(bool disposing)
        {
            Debug.Assert(Counter.Count == 0);

            // disposing == false when finilizing and detected that non pooled
            if (disposing)
            {
                // will call OnNoRef if last borrower
                // Unpin();
                ClearBeforePooling();

                // try to pool
                bool pooled = false;
                if (_pool != null)
                {
#pragma warning disable 618
                    
                    pooled = _pool.Return(this);
#pragma warning restore 618
                }

                if (!pooled)
                {
                    GC.SuppressFinalize(this);
                    Dispose(false);
                }

            }
            else
            {
                Debug.Assert(Counter.Count == 0);
                // this calls _counter.Dispose() and clears _pointer & _capacity;
                base.Dispose(false);
                // Dispose destructs this object and native buffer
                _offHeapBuffer.Dispose();
                // set all to default, increase chances to detect errors
                _offHeapBuffer = default;

                // either finalizing non-pooled or pool if full
                AtomicCounterService.ReleaseCounter(Counter);
                // Counter = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Init(int minimumCapacity)
        {
            //var newcount = Counter.Increment();
            //if (newcount > 1)
            //{
            //    ThrowHelper.ThrowInvalidOperationException("Cannot change off-heap buffer capacity when retained.");
            //}

            //if (newcount <= 0)
            //{
            //    ThrowHelper.ThrowObjectDisposedException("OffHeapMemory");
            //}

            _offHeapBuffer.EnsureCapacity(minimumCapacity);

            _pointer = _offHeapBuffer._pointer;
            _capacity = _offHeapBuffer.Length;
        }
    }
}
