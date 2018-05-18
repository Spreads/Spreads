// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using SRCS = System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    // NB (over)Using [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)] just in case it could help sealed devirt

    public sealed class OwnedPooledArray<T> : MemoryManager<T>
    {
        private static readonly ObjectPool<OwnedPooledArray<T>> Pool = new ObjectPool<OwnedPooledArray<T>>(() => new OwnedPooledArray<T>(), Environment.ProcessorCount * 16);

        private T[] _array;
        internal int _referenceCount;

        private OwnedPooledArray()
        { }

        [Obsolete("Use TryGetArray")]
        public T[] Array
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get { return _array; }
        }

        public int Length
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get { return _array.Length; }
        }

        public bool IsDisposed
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get { return _array == null; }
        }

        public bool IsRetained
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get { return _referenceCount > 0; }
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public override Span<T> GetSpan()
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            return new Span<T>(_array);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static OwnedPooledArray<T> Create(int minLength)
        {
            var ownedPooledArray = Pool.Allocate();
            ownedPooledArray._array = BufferPool<T>.Rent(minLength);
            ownedPooledArray._referenceCount = 0;
            return ownedPooledArray;
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static OwnedPooledArray<T> Create(T[] array)
        {
            var ownedPooledArray = Pool.Allocate();
            ownedPooledArray._array = array;
            ownedPooledArray._referenceCount = 0;
            return ownedPooledArray;
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        protected override void Dispose(bool disposing)
        {
            // special value that is not possible normally to keep thread-static buffer undisposable
            if (_referenceCount == int.MinValue) { return; }

            if (_referenceCount > 0)
            {
                // ThrowHelper.ThrowInvalidOperationException("Disposing an OwnedPooledArray with ratained references");
                Unpin();
                return;
            }

            var array = Interlocked.Exchange(ref _array, null);
            if (array == null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            }
            BufferPool<T>.Return(array);
            if (disposing)
            {
                Pool.Free(this);
            }
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public new bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            buffer = new ArraySegment<T>(_array);
            return true;
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            unsafe
            {
                if (IsDisposed) { ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>)); }

                if (_referenceCount != int.MinValue)
                {
                    Interlocked.Increment(ref _referenceCount);
                }

                if ((uint)elementIndex > (uint)_array.Length) { ThrowHelper.ThrowArgumentOutOfRangeException(nameof(elementIndex)); }
                var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                var pointer = SRCS.Unsafe.Add<T>((void*)handle.AddrOfPinnedObject(), elementIndex);
                return new MemoryHandle(pointer, handle, this);
            }
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain()
        {
            return RetainImpl();
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int length)
        {
            return RetainImpl(length: length);
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int start, int length)
        {
            return RetainImpl(start, length);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        private RetainedMemory<T> RetainImpl(int start = -1, int length = -1)
        {
            if (IsDisposed) { ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>)); }

            if (_referenceCount != int.MinValue)
            {
                Interlocked.Increment(ref _referenceCount);
            }

            unsafe
            {
                // MemoryHandle is not exposed in RetainedMemory. It is just IDisposable that
                // keeps MH with it and could decrement refcount of this object when disposed.
                // We sometimes need to keep a reference to underlying array and avoid returning
                // it to BufferPool.
                var handle = new MemoryHandle((void*)IntPtr.Zero, default, this);
                if (length < 0) return new RetainedMemory<T>(Memory, handle);
                if (start >= 0)
                {
                    return new RetainedMemory<T>(CreateMemory(start, length), handle);
                }
                return new RetainedMemory<T>(CreateMemory(length), handle);
            }
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public override void Unpin()
        {
            // special value that is not possible normally to keep thread-static buffer undisposable
            if (_referenceCount == int.MinValue) { return; }
            var newRefCount = Interlocked.Decrement(ref _referenceCount);
            if (newRefCount < 0) { ThrowHelper.ThrowInvalidOperationException(); }
            if (newRefCount == 0)
            {
                Dispose(true);
            }
        }
    }
}
