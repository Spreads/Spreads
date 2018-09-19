// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Buffers
{
    public sealed class OwnedPooledArray<T> : MemoryManager<T>
    {
        private static readonly ObjectPool<OwnedPooledArray<T>> Pool = new ObjectPool<OwnedPooledArray<T>>(() => new OwnedPooledArray<T>(), Environment.ProcessorCount * 16);

        private T[] _array;
        internal int _referenceCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private OwnedPooledArray()
        { }

        internal T[] Array
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array == null;
        }

        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _referenceCount > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Span<T> GetSpan()
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            return new Span<T>(_array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OwnedPooledArray<T> Create(int minLength)
        {
            var ownedPooledArray = Pool.Allocate();
            ownedPooledArray._array = BufferPool<T>.Rent(minLength);
            ownedPooledArray._referenceCount = 0;
            return ownedPooledArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OwnedPooledArray<T> Create(T[] array)
        {
            var ownedPooledArray = Pool.Allocate();
            ownedPooledArray._array = array;
            ownedPooledArray._referenceCount = 0;
            return ownedPooledArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void Dispose(bool disposing)
        {
            // special value that is not normally possible - to keep thread-static buffer undisposable
            if (_referenceCount == int.MinValue) { return; }

            if (_referenceCount > 0)
            {
                Unpin();
                return;
            }

            if (_referenceCount < 0)
            {
                FailNegativeRefCount();
            }

            var array = Interlocked.Exchange(ref _array, null);
            if (array == null)
            {
                ThrowObjectDisposedException();
            }
            BufferPool<T>.Return(array, false);
            if (disposing)
            {
                Pool.Free(this);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailNegativeRefCount()
        {
            ThrowHelper.FailFast("OwnedPooledArray.Dispose: _referenceCount < 0");
        }

        ~OwnedPooledArray()
        {
            // NB MemoryManager calls SuppressFinalize in Dispose(true)
            Dispose(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            buffer = new ArraySegment<T>(_array);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            unsafe
            {
                if (IsDisposed) { ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>)); }

                if (_referenceCount != int.MinValue)
                {
                    Interlocked.Increment(ref _referenceCount);
                }

                if ((uint)elementIndex > (uint)_array.Length)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(elementIndex));
                }

                var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);

                var pointer = Unsafe.Add<T>((void*)handle.AddrOfPinnedObject(), elementIndex);

                return new MemoryHandle(pointer, handle, this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Unpin()
        {
            // special value that is not possible normally - to keep thread-static buffer undisposable
            if (_referenceCount == int.MinValue) { return; }

            var newRefCount = Interlocked.Decrement(ref _referenceCount);
            if (newRefCount == 0)
            {
                Dispose(true);
            }
            if (newRefCount < 0) { ThrowHelper.FailFast("Buffer refcount was already zero before unpin."); }
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain()
        {
            return RetainImpl();
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int length)
        {
            if ((uint)length > (uint)_array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
            }
            return RetainImpl(length: length);
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int start, int length)
        {
            if ((uint)start + (uint)length > (uint)_array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));
            }
            return RetainImpl(start, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RetainedMemory<T> RetainImpl(int start = 0, int length = -1)
        {
            if (IsDisposed)
            {
                ThrowObjectDisposedException();
            }

            if (_referenceCount != int.MinValue)
            {
                Interlocked.Increment(ref _referenceCount);
            }

            unsafe
            {
                // MemoryHandle is not exposed in RetainedMemory. It is just IDisposable that
                // keeps MH with it and could decrement refcount of this object when disposed.
                // We sometimes need to keep a reference to underlying buffer and avoid returning
                // it to a BufferPool.

                var handle = new MemoryHandle((void*)IntPtr.Zero, default, this);
                var mem = length < 0 ? Memory : CreateMemory(start, length);
                return new RetainedMemory<T>(mem, handle);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowObjectDisposedException()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
        }
    }
}
