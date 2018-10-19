// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.Buffers
{
    public class OffHeapMemory<T> : UnmanagedMemory<T> where T : unmanaged
    {
        private OffHeapBuffer<T> _offHeapBuffer;
        private readonly OffHeapBufferPool<T> _pool;

        internal OffHeapMemory(OffHeapBuffer<T> offHeapBuffer, OffHeapBufferPool<T> pool)
        {
            _offHeapBuffer = offHeapBuffer;
            _pool = pool;
            InternalDirectBuffer = _offHeapBuffer._db;
        }

        protected override void OnNoReferences()
        {
            _pool.Return(this);
        }

        protected override void Dispose(bool disposing)
        {
            _offHeapBuffer.Dispose();
            base.Dispose(disposing);
        }

        internal void EnsureCapacity(int minimumCapacity)
        {
            _offHeapBuffer.EnsureCapacity(minimumCapacity);
        }
    }

    public class OffHeapMemory : UnmanagedMemory<byte>
    {
        private OffHeapBuffer<byte> _offHeapBuffer;
        private readonly OffHeapBufferPool _pool;

        internal OffHeapMemory(OffHeapBuffer<byte> offHeapBuffer, OffHeapBufferPool pool)
        {
            _offHeapBuffer = offHeapBuffer;
            _pool = pool;
            InternalDirectBuffer = _offHeapBuffer._db;
        }

        protected override void OnNoReferences()
        {
            var pooled = _pool.Return(this);

            if (!pooled)
            {
                if (!IsDisposed)
                {
                    ThrowHelper.ThrowInvalidOperationException("_pool.Return must dispose OffHeapMemory if no capacity or too big");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            _offHeapBuffer.Dispose();
            base.Dispose(disposing);
        }

        internal void EnsureCapacity(int minimumCapacity)
        {
            _offHeapBuffer.EnsureCapacity(minimumCapacity);
            InternalDirectBuffer = _offHeapBuffer._db;
        }

        ~OffHeapMemory()
        {
            Dispose(false);
        }
    }
}
