// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Native;
using Spreads.Serialization;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Collections;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    /// <summary>
    /// <see cref="RetainableMemory{T}"/> backed by an array.
    /// </summary>
    [Obsolete("TODO Comment this out, remove usages when pooled PM should be used, then keep it only as a wrapper for external CLR arrays")]
    public sealed class ArrayMemory<T> : RetainableMemory<T>
    {
        private static readonly ObjectPool<ArrayMemory<T>> ObjectPool = new ObjectPool<ArrayMemory<T>>(() => new ArrayMemory<T>(), perCoreSize: 16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayMemory()
        {
        }

        internal T[] Array
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.As<T[]>(_array);
        }

        public ArraySegment<T> ArraySegment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ArraySegment<T>(Unsafe.As<T[]>(_array), _offset, _length);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by an array from shared array pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayMemory<T> Create(int minLength)
        {
            var array = BufferPool<T>.Rent(minLength);
            return Create(array, offset: 0, array.Length, externallyOwned: false);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArrayMemory<T> Create(RetainableMemoryPool<T> pool, int minLength)
        {
            var array = BufferPool<T>.Rent(minLength);
            return Create(array, offset: 0, array.Length, externallyOwned: false, pool);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayMemory<T> Create(T[] array)
        {
            return Create(array, offset: 0, array.Length, externallyOwned: true);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArrayMemory<T> Create(T[] array, int offset, int length, bool externallyOwned, RetainableMemoryPool<T> pool = null)
        {
            var arrayMemory = ObjectPool.Rent();
            arrayMemory._array = array;
            arrayMemory._pointer = IntPtr.Zero;
            arrayMemory._offset = offset;
            arrayMemory._length = length;
            arrayMemory.PoolIndex =
                pool is null
                    ? externallyOwned ? (byte) 0 : (byte) 1
                    : pool.PoolIdx;

            // Clear counter (with flags, AM does not have any flags).
            // We cannot tell if ObjectPool allocated a new one or took from pool
            // other then by checking if the counter is disposed, so we cannot require
            // that the counter is disposed. We only need that pooled object has the counter
            // in disposed state so that no one accidentally uses the object while it is in the pool.
            arrayMemory.CounterRef = 0;

            return arrayMemory;
        }

        /// <summary>
        /// Returns <see cref="Vec{T}"/> backed by this instance memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Vec<T> GetVec()
        {
            if (IsDisposed)
                ThrowDisposed<ArrayMemory<T>>();
            var vec = new Vec<T>(Unsafe.As<T[]>(_array), _offset, _length);
#if SPREADS
            ThrowHelper.DebugAssert(vec.AsVec().ItemType == typeof(T));
#endif
            return vec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Span<T> GetSpan()
        {
            if (IsDisposed)
                ThrowDisposed<ArrayMemory<T>>();

            return new Span<T>(Unsafe.As<T[]>(_array), _offset, _length);
        }

        [Obsolete("Prefer fixed statements on a pinnable reference for short-lived pinning")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override unsafe MemoryHandle Pin(int elementIndex = 0)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        {
            if (!TypeHelper<T>.IsPinnable)
                ThrowNotPinnable();

            var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
            var pointer = Unsafe.AsPointer(ref Unsafe.As<T[]>(_array)[_offset + elementIndex]);
            Increment();

            return new MemoryHandle(pointer, handle, this);
        }

        internal override void Free(bool finalizing)
        {
            ThrowHelper.Assert(IsDisposed);
            ThrowHelper.Assert(_pointer == IntPtr.Zero);

            var array = Interlocked.Exchange(ref _array, null);
            if (array != null)
            {
                if(!finalizing && !IsExternallyOwned)
                    BufferPool<T>.Return(Unsafe.As<T[]>(array), clearArray: true);
            }

            if (array == null)
            {
                string msg = "Tried to free already freed ArrayMemory";
#if DEBUG
                ThrowHelper.ThrowInvalidOperationException(msg);
#endif
                Trace.TraceWarning(msg);
                return;
            }

            ClearFields();

            // See PrivateMemory comment in the same place
            if (!finalizing)
                ObjectPool.Return(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed)
                ThrowDisposed<ArrayMemory<T>>();

            buffer = new ArraySegment<T>(Unsafe.As<T[]>(_array), _offset, _length);
            return true;
        }
    }
}