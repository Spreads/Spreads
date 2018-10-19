// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Buffers
{
    public abstract unsafe class UnmanagedMemory<T> : MemoryManager<T> where T : unmanaged
    {
        private int _referenceCount;
        private bool _disposed;

        internal DirectBuffer InternalDirectBuffer;

        internal void Increment()
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(UnmanagedMemory<T>));
            Interlocked.Increment(ref _referenceCount);
        }

        internal bool Decrement()
        {
            int newRefCount = Interlocked.Decrement(ref _referenceCount);
            if (newRefCount < 0) ThrowHelper.ThrowInvalidOperationException();
            if (newRefCount == 0)
            {
                OnNoReferences();
                return false;
            }
            return true;
        }

        internal bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _referenceCount > 0;
        }

        protected virtual void OnNoReferences()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (IsRetained)
            {
                ThrowDisposingRetained();
            }
            _disposed = disposing;
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _disposed;
        }

        public long Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InternalDirectBuffer.LongLength;
        }

        public override Span<T> GetSpan()
        {
            return MemoryMarshal.Cast<byte, T>(InternalDirectBuffer.Span);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            Increment();
            if (elementIndex < 0 || elementIndex > Capacity) throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(Unsafe.Add<byte>(InternalDirectBuffer.Data, elementIndex), default, this);
        }

        internal MemoryHandle GetHandleNoIncrement(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= Capacity) throw new ArgumentOutOfRangeException(nameof(elementIndex));
            return new MemoryHandle(Unsafe.Add<byte>(InternalDirectBuffer.Data, elementIndex), default, this);
        }

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
            if ((uint)length > (uint)Capacity)
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
            if ((uint)start + (uint)length > (uint)Capacity)
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

            var handle = new MemoryHandle(InternalDirectBuffer.Data, default, this);
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadLength()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException("length");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(UnmanagedMemory<T>));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposingRetained()
        {
            ThrowHelper.ThrowInvalidOperationException("Cannot dipose retained " + nameof(UnmanagedMemory<T>));
        }
    }
}
