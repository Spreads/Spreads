// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Buffers;

namespace Spreads.Buffers
{
    public unsafe class OffHeapMemory<T> : RetainableMemory<T> where T : struct
    {
        private OffHeapBuffer<T> _offHeapBuffer;
        private readonly OffHeapBufferPool<T> _pool;

        internal OffHeapMemory(OffHeapBuffer<T> offHeapBuffer, OffHeapBufferPool<T> pool) :
            base(AtomicCounterService.AcquireCounter())
        {
            _offHeapBuffer = offHeapBuffer;
            _pool = pool;
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            Increment();
            return new MemoryHandle(_offHeapBuffer._pointer, default, this);
            //public override MemoryHandle Pin(int elementIndex = 0)
            //{
            //    Increment();
            //    if (elementIndex < 0 || elementIndex > _capacity) throw new ArgumentOutOfRangeException(nameof(elementIndex));
            //    return new MemoryHandle(Unsafe.Add<byte>(InternalDirectBuffer.Data, elementIndex), default, this);
            //}
        }

        public override void Unpin()
        {
            Decrement();
        }

        protected override void OnNoReferences()
        {
            _pool.Return(this);
        }

        protected override void Dispose(bool disposing)
        {
            // Dispose destructs this object and native buffer
            _offHeapBuffer.Dispose();
            _counter.Dispose();
            AtomicCounterService.ReleaseCounter(_counter);
            base.Dispose(disposing);
        }

        internal void EnsureCapacity(int minimumCapacity)
        {
            if (IsRetained)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot change off-heap buffer capacity when retained.");
            }
            _offHeapBuffer.EnsureCapacity(minimumCapacity);
        }
    }
}
