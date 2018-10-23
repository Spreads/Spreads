// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    /// <summary>
    /// Base class for retainable pinned memory. Buffers are always pinned during initialization.
    /// Initialization could be from a pool of arrays or from native memory. When pooled, RetainableMemory
    /// is in disposed state, underlying arrays are unpinned and returned to array pool.
    /// </summary>
    public abstract unsafe class RetainableMemory<T> : MemoryManager<T>
    {
        internal AtomicCounter Counter;

        // [p*_______[idx<---len--->]___] we must only check capacity at construction and then work from pointer

        protected int _offset;
        protected int _length;
        protected void* _pointer;

        protected RetainableMemory(AtomicCounter counter)
        {
            if (counter.Count != 0)
            {
                ThrowHelper.ThrowArgumentException("counter.Count != 0");
            }
            Counter = counter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Increment()
        {
            return Counter.Increment();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Decrement()
        {
            var newRefCount = Counter.Decrement();
            if (newRefCount == 0)
            {
                OnNoReferences();
                return false;
            }
            return true;
        }

        internal DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(_length * Unsafe.SizeOf<T>(), (byte*)_pointer);
        }

        internal void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer;
        }

        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Counter.Count > 0;
        }

        public int ReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Counter.Count;
        }

        protected void OnNoReferences()
        {
            // Pooled implementation try to return object to pool
            // If not possible to tell if object is returned then rely on finalizer
            // In steady-state that should not be a big deal
            Dispose(true);
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Counter.IsDisposed;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public override Span<T> GetSpan()
        {
            return new Span<T>(_pointer, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // hope for devirt
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            Increment();
            if (unchecked((uint)elementIndex) >= _length) // if (elementIndex < 0 || elementIndex >= _capacity)
            {
                ThrowIndexOutOfRange();
            }

            // NOTE: even for the array-based memory handle is create when array is taken from pool
            // and is stored in MemoryManager until the array is released back to the pool.
            GCHandle handle = default;
            return new MemoryHandle(Unsafe.Add<T>(_pointer, _offset + elementIndex), handle, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // hope for devirt
        public override void Unpin()
        {
            Decrement();
        }

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
            if ((uint)length > (uint)_length)
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
            if ((uint)start + (uint)length > (uint)_length)
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
                ThrowDisposed<RetainedMemory<T>>();
            }

            if (length < 0)
            {
                length = Length;
            }

            if (start < 0)
            {
                start = 0;
            }

            return new RetainedMemory<T>(this, start, length, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearBeforePooling()
        {
            _pointer = null;
            _length = default;
            _offset = default;
        }

        protected override void Dispose(bool disposing)
        {
            Counter.Dispose();
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~RetainableMemory()
        {
            Dispose(false);
        }
    }
}
