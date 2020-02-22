// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Native;
using Spreads.Serialization;
using Spreads.Threading;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    public class ArrayMemory<T> : RetainableMemory<T>
    {
        private static readonly ObjectPool<ArrayMemory<T>> ObjectPool = new ObjectPool<ArrayMemory<T>>(() => new ArrayMemory<T>(), Environment.ProcessorCount * 16);

        protected GCHandle _handle;
        internal T[] _array;
        internal int _arrayOffset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ArrayMemory()
        { }

        internal T[] Array
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array;
        }

        public ArraySegment<T> ArraySegment
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ArraySegment<T>(_array, _arrayOffset, _length);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by an array from shared array pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayMemory<T> Create(int minLength, bool pin = false)
        {
            return Create(BufferPool<T>.Rent(minLength), externallyOwned: false, pin);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArrayMemory<T> Create(T[] array, bool externallyOwned, bool pin = false)
        {
            return Create(array, 0, array.Length, externallyOwned, pin);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe ArrayMemory<T> Create(T[] array, int offset, int length, bool externallyOwned, bool pin, RetainableMemoryPool<T> pool = null)
        {
            var arrayMemory = ObjectPool.Rent();
            arrayMemory._array = array;

            if (pin)
            {
                if (!TypeHelper<T>.IsPinnable)
                {
                    ThrowNotPinnable();
                }

                arrayMemory._handle = GCHandle.Alloc(arrayMemory._array, GCHandleType.Pinned);
                arrayMemory._pointer = Unsafe.AsPointer(ref arrayMemory._array[offset]);
            }
            else
            {
                arrayMemory._pointer = null;
            }

            arrayMemory._arrayOffset = offset;
            arrayMemory._length = length;
            arrayMemory.PoolIndex =
                pool is null
                ? externallyOwned ? (byte)0 : (byte)1
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
        public sealed override unsafe Vec<T> Vec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // var tid = VecTypeHelper<T>.RuntimeVecInfo.RuntimeTypeId;
                var vec = _pointer == null ? new Vec<T>(_array, _arrayOffset, _length) : new Vec<T>(_pointer, _length);
#if SPREADS
                ThrowHelper.DebugAssert(vec.AsVec().ItemType == typeof(T));
#endif
                return vec;
            }
        }

        [Obsolete("Prefer fixed statements on a pinnable reference for short-lived pinning")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override unsafe MemoryHandle Pin(int elementIndex = 0)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        {
            if (_pointer == null)
            {
                if (!TypeHelper<T>.IsPinnable)
                {
                    ThrowNotPinnable();
                }

                Increment();
                var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                var pointer = Unsafe.AsPointer(ref _array[_arrayOffset + elementIndex]);
                return new MemoryHandle(pointer, handle, this);
            }

            return base.Pin(elementIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe Span<T> GetSpan()
        {
            if (IsPooled)
            {
                ThrowDisposed<ArrayMemory<T>>();
            }

            // if disposed Pointer & _len are null/0, no way to corrupt data, will just throw
            if (_pointer == null)
            {
                return new Span<T>(_array, _arrayOffset, _length);
            }

            return new Span<T>(_pointer, _length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotPinnable()
        {
            ThrowHelper.ThrowInvalidOperationException($"Type {typeof(T).Name} is not pinnable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override unsafe void Dispose(bool disposing)
        {
            if (disposing)
            {
                var pool = Pool;
                if (pool != null)
                {
                    // throws if ref count is not zero
                    pool.ReturnInternal(this, clearMemory: TypeHelper<T>.IsReferenceOrContainsReferences);
                    // pool calls Dispose(false) if a bucket is full
                    return;
                }

                // not poolable, doing finalization work now
                GC.SuppressFinalize(this);
            }

            AtomicCounter.Dispose(ref CounterRef);

            // Finalization

            ThrowHelper.DebugAssert(!IsPooled);

            var array = _array;
            _array = null;
            if (array != null)
            {
                ClearAfterDispose();
                if (!ExternallyOwned)
                {
                    BufferPool<T>.Return(array, clearArray: TypeHelper<T>.IsReferenceOrContainsReferences);
                }

                ThrowHelper.DebugAssert(_pointer == null || _handle.IsAllocated);
                if (_handle.IsAllocated)
                {
                    _handle.Free();
                    _handle = default;
                }

                _handle = default;
                _arrayOffset = -1; // make it unusable if not re-initialized
                PoolIndex = default; // after ExternallyOwned check!
            }
            else if (disposing)
            {
                // when no pinned we do not create a memory handle
                ThrowDisposed<ArrayMemory<T>>();
            }

            ThrowHelper.DebugAssert(!_handle.IsAllocated);

            // We cannot tell if this object is pooled, so we rely on finalizer
            // that will be called only if the object is not in the pool.
            // But if we tried to pool the buffer to RMP but failed above
            // then we called GC.SuppressFinalize(this)
            // and finalizer won't be called if the object is dropped from ObjectPool.
            // We have done buffer clean-up job and this object could die normally.
            ObjectPool.Return(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed) { ThrowDisposed<ArrayMemory<T>>(); }
            buffer = new ArraySegment<T>(_array, _arrayOffset, _length);
            return true;
        }
    }
}
