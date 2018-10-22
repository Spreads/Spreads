// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    public sealed unsafe class ArrayMemory<T> : RetainableMemory<T>
    {
        private static readonly ObjectPool<ArrayMemory<T>> Pool = new ObjectPool<ArrayMemory<T>>(() => new ArrayMemory<T>(), Environment.ProcessorCount * 16);

        private T[] _array;
        private GCHandle _handle;
        private bool _externallyOwned;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayMemory() : base(AtomicCounterService.AcquireCounter())
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

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by an array from shared array pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayMemory<T> Create(int minLength)
        {
            return Create(BufferPool<T>.Rent(minLength));
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// Ownership of the provided array if transafered to <see cref="ArrayMemory{T}"/> after calling
        /// this method and no other code should touch the array afterwards.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayMemory<T> Create(T[] array)
        {
            return Create(array, false);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArrayMemory<T> Create(T[] array, bool externallyOwned)
        {
            var ownedPooledArray = Pool.Allocate();
            ownedPooledArray._array = array;
            //ownedPooledArray._handle = GCHandle.Alloc(ownedPooledArray._array, GCHandleType.Pinned);
            //ownedPooledArray._pointer = Unsafe.AsPointer(ref ownedPooledArray._array[0]);
            ownedPooledArray._length = array.Length;
            ownedPooledArray._externallyOwned = externallyOwned;
            return ownedPooledArray;
        }

        // TODO review if we need this object unpinned and why we cannot work with raw arrays in that case
        //[MethodImpl(MethodImplOptions.AggressiveInlining)] // hope for devirt
        //public override MemoryHandle Pin(int elementIndex = 0)
        //{
        //    Debug.Assert(_handle != default && _pointer != null);
        //    if (_pointer == null)
        //    {
        //        Debug.Assert(_handle == default);
        //        var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
        //        var pointer = Unsafe.AsPointer(ref _array[0]);
        //        Interlocked.CompareExchange(ref Unsafe.AsRef(_pointer), )
        //        _pointer = Unsafe.AsPointer(ref _array[0]); // (void*)_handle.AddrOfPinnedObject();
        //    }
        //    return base.Pin(elementIndex);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void Dispose(bool disposing)
        {
            // special value that is not normally possible - to keep thread-static buffer undisposable
            if (_externallyOwned) { return; }

            if (IsRetained)
            {
                ThrowDisposingRetained<ArrayMemory<T>>();
            }
            
            var array = Interlocked.Exchange(ref _array, null);
            if (array != null)
            {
                ClearBeforePooling();
                //Debug.Assert(_handle.IsAllocated);
                //_handle.Free();
                //_handle = default;
                BufferPool<T>.Return(array, !TypeHelper<T>.IsFixedSize);
            }

            // Debug.Assert(!_handle.IsAllocated);

            if (disposing)
            {
                if (array == null)
                {
                    ThrowDisposed<ArrayMemory<T>>();
                }

                // we cannot tell is this object is pooled, so we rely on finalizer
                // that will be called only if the object is not in the pool
                Pool.Free(this);
            }
            else
            {
                base.Dispose(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(ArrayMemory<T>));
            buffer = new ArraySegment<T>(_array);
            return true;
        }
    }
}
