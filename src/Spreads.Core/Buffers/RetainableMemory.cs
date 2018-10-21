// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    /// <summary>
    /// Base class for retainable pinned memory. Buffers are always pinned during initialization.
    /// Initialization could be from a pool of arrays or from native memory. When pooled, RetainableMemory
    /// is in disposed state, underlying arrays are unpinned and returned to array pool.
    /// </summary>
    public abstract unsafe class RetainableMemory<T> : MemoryManager<T>
    {
        protected readonly AtomicCounter _counter;
        protected int _capacity;
        protected void* _pointer;

        internal DirectBuffer DirectBuffer => new DirectBuffer(_capacity * Unsafe.SizeOf<T>(), (byte*)_pointer);

        protected RetainableMemory(AtomicCounter counter)
        {
            _counter = counter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Increment()
        {
            return _counter.Increment();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Decrement()
        {
            var newRefCount = _counter.Decrement();
            if (newRefCount == 0)
            {
                OnNoReferences();
                return false;
            }
            return true;
        }

        internal void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer;
        }

        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _counter.Count > 0;
        }

        public int ReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _counter.Count;
        }

        protected virtual void OnNoReferences()
        {
            // Pooled implementation try to return object to pool
            // If not possible to tell if object is returned then rely on finalizer
            // In steady-state that should not be a big deal
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _counter.IsDisposed;
        }

        public long Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _capacity;
        }

        public override Span<T> GetSpan()
        {
            return new Span<T>(_pointer, _capacity);
        }

        //public override MemoryHandle Pin(int elementIndex = 0)
        //{
        //    Increment();
        //    if (elementIndex < 0 || elementIndex > _capacity) throw new ArgumentOutOfRangeException(nameof(elementIndex));
        //    return new MemoryHandle(Unsafe.Add<byte>(InternalDirectBuffer.Data, elementIndex), default, this);
        //}

        //internal MemoryHandle GetHandleNoIncrement(int elementIndex = 0)
        //{
        //    if (elementIndex < 0 || elementIndex >= Capacity) throw new ArgumentOutOfRangeException(nameof(elementIndex));
        //    return new MemoryHandle(Unsafe.Add<byte>(InternalDirectBuffer.Data, elementIndex), default, this);
        //}

        //public override void Unpin()
        //{
        //    Decrement();
        //}

        /// <summary>
        /// Retain buffer memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain()
        {
            return RetainImpl();
        }

        /// <summary>
        /// Retain buffer memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int length)
        {
            if ((uint)length > (uint)_capacity)
            {
                ThrowBadLength();
            }

            return RetainImpl(length: length);
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int start, int length)
        {
            if ((uint)start + (uint)length > (uint)_capacity)
            {
                ThrowBadLength();
            }
            return RetainImpl(start, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RetainedMemory<T> RetainImpl(int start = -1, int length = -1)
        {
            if (IsDisposed)
            {
                ThrowDisposed();
            }

            Increment();

            var handle = new MemoryHandle(DirectBuffer.Data, default, this);
            Memory<T> memory;
            if (length < 0)
            {
                memory = Memory;
            }
            else if (start >= 0)
            {
                memory = CreateMemory(start, length);
            }
            else
            {
                memory = CreateMemory(length);
            }

            return new RetainedMemory<T>(memory, handle);
        }

        protected override void Dispose(bool disposing)
        {
            _pointer = null;
            _counter.Dispose();
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~RetainableMemory()
        {
            Dispose(false);
        }

        #region ThrowHelpers

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadLength()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException("length");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(RetainableMemory<T>));
        }


        // TODO delete
        //[MethodImpl(MethodImplOptions.NoInlining)]
        //private static void ThrowDisposingRetained()
        //{
        //    ThrowHelper.ThrowInvalidOperationException("Cannot dipose retained " + nameof(RetainableMemory<T>));
        //}

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //private static void ThowNegativeRefCount()
        //{
        //    ThrowHelper.ThrowInvalidOperationException("_referenceCount < 0");
        //}

        #endregion ThrowHelpers
    }
}
