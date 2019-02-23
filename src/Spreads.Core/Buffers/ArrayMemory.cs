// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Native;
using Spreads.Serialization;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    public class ArrayMemorySliceBucket<T>
    {
        private ArrayMemory<T> _slab;
        private int _slabFreeCount;

        private readonly int _bufferLength;
        private readonly LockedObjectPool<ArrayMemorySlice<T>> _pool;

        public ArrayMemorySliceBucket(int bufferLength, int maxBufferCount)
        {
            if (!BitUtil.IsPowerOfTwo(bufferLength) || bufferLength >= Settings.SlabLength)
            {
                ThrowHelper.ThrowArgumentException("bufferLength must be a power of two max 64kb");
            }

            _bufferLength = bufferLength;
            // NOTE: allocateOnEmpty = true
            _pool = new LockedObjectPool<ArrayMemorySlice<T>>(maxBufferCount, Factory, allocateOnEmpty: true);

            _slab = ArrayMemory<T>.Create(Settings.SlabLength, true);
            _slabFreeCount = _slab.Length / _bufferLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayMemorySlice<T> RentMemory()
        {
            return _pool.Rent();
        }

        private ArrayMemorySlice<T> Factory()
        {
            // the whole purpose is pooling of ArrayMemorySlice, it's OK to lock
            lock (_pool)
            {
                if (_slabFreeCount == 0)
                {
                    // drop previous slab, it is owned by previous slices
                    // and will be returned to the pool when all slices are disposed
                    _slab = ArrayMemory<T>.Create(Settings.SlabLength, true);
                    _slabFreeCount = _slab.Length / _bufferLength;
                }

                var offset = _slab.Length - _slabFreeCount-- * _bufferLength;

                var slice = new ArrayMemorySlice<T>(_slab, _pool, offset, _bufferLength);
                return slice;
            }
        }
    }

    public class ArrayMemorySlice<T> : ArrayMemory<T>
    {
        [Obsolete("internal only for tests/disgnostics")]
        internal readonly ArrayMemory<T> _slab;

        private readonly LockedObjectPool<ArrayMemorySlice<T>> _slicesPool;

        public unsafe ArrayMemorySlice(ArrayMemory<T> slab, LockedObjectPool<ArrayMemorySlice<T>> slicesPool, int offset, int length)
        {
            if (!TypeHelper<T>.IsPinnable)
            {
                ThrowHelper.FailFast("Do not use slices for not pinnable");
            }

#pragma warning disable 618
            _slab = slab;
            _slab.Increment();
            _pointer = Unsafe.Add<T>(_slab.Pointer, offset);
            _handle = GCHandle.Alloc(_slab);
#pragma warning restore 618
            _slicesPool = slicesPool;
            _length = length;
            _array = slab._array;
            _arrayOffset = slab._arrayOffset + offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void Dispose(bool disposing)
        {
            if (ExternallyOwned)
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            if (disposing)
            {
                if (_isPooled)
                {
                    ThrowAlreadyPooled<RetainableMemory<T>>();
                }

                Pool?.ReturnNoChecks(this, clearMemory: !TypeHelper<T>.IsPinnable);

                if (_isPooled)
                {
                    return;
                }
                // not pooled, doing finalization work now
                GC.SuppressFinalize(this);
            }

            // Finalization

            AtomicCounter.Dispose(ref CounterRef);

            Debug.Assert(!_isPooled);
            _poolIdx = default;

            // we still could add this to the pool of free pinned slices that are backed by an existing slab
            var pooledToFreeSlicesPool = _slicesPool.Return(this);
            if (pooledToFreeSlicesPool)
            {
                return;
            }

            var array = Interlocked.Exchange(ref _array, null);
            if (array != null)
            {
                ClearAfterDispose();
                Debug.Assert(_handle.IsAllocated);
#pragma warning disable 618
                _slab.Decrement();
                _handle.Free();
#pragma warning restore 618
            }
            else
            {
                ThrowDisposed<ArrayMemorySlice<T>>();
            }

            Debug.Assert(!_handle.IsAllocated);
        }
    }

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
        public static ArrayMemory<T> Create(int minLength, bool pin)
        {
            return Create(BufferPool<T>.Rent(minLength), false, pin);
        }

        /// <summary>
        /// Create <see cref="ArrayMemory{T}"/> backed by the provided array.
        /// Ownership of the provided array if transferred to <see cref="ArrayMemory{T}"/> after calling
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
            var arrayMemory = ObjectPool.Allocate();
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
                arrayMemory._handle = GCHandle.Alloc(arrayMemory._array, GCHandleType.Normal);
                arrayMemory._pointer = null;
            }

            arrayMemory._arrayOffset = offset;
            arrayMemory._length = length;
            // arrayMemory._externallyOwned = externallyOwned;
            arrayMemory._poolIdx =
                pool is null
                ? externallyOwned ? (byte)0 : (byte)1
                : pool.PoolIdx;

            // Clear counter. We cannot tell if ObjectPool allocated a new one or took from pool
            // other then by checking if the counter is disposed, so we cannot require
            // that the counter is disposed. We only need that pooled object has the counter
            // in disposed state so that no-one accidentally uses the object while it is in the pool.
            // Just clear it now
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
                Debug.Assert(vec.AsVec().Type == typeof(T));
                return vec;
            }
        }

        public override unsafe Span<T> GetSpan()
        {
            if (_isPooled)
            {
                ThrowDisposed<RetainableMemory<T>>();
            }

            // if disposed Pointer & _len are null/0, no way to corrupt data, will just throw
            return _pointer == null ? new Span<T>(_array, _arrayOffset, _length) : new Span<T>(Pointer, _length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotPinnable()
        {
            ThrowHelper.ThrowInvalidOperationException($"Type {typeof(T).Name} is not pinnable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isPooled)
                {
                    ThrowAlreadyPooled<RetainableMemory<T>>();
                }

                Pool?.ReturnNoChecks(this, clearMemory: !TypeHelper<T>.IsPinnable);

                if (_isPooled)
                {
                    return;
                }
                // not pooled, doing finalization work now
                GC.SuppressFinalize(this);
            }

            // Finalization

            AtomicCounter.Dispose(ref CounterRef);

            Debug.Assert(!_isPooled);

            var array = Interlocked.Exchange(ref _array, null);
            if (array != null)
            {
                ClearAfterDispose();
                if (!ExternallyOwned)
                {
                    BufferPool<T>.Return(array, !TypeHelper<T>.IsFixedSize);
                }
                Debug.Assert(_handle.IsAllocated);
                _handle.Free();
                _handle = default;
                _arrayOffset = -1; // make it unusable if not re-initialized
                _poolIdx = default; // after ExternallyOwned check!
            }
            else
            {
                ThrowDisposed<ArrayMemory<T>>();
            }

            Debug.Assert(!_handle.IsAllocated);

            // We cannot tell if this object is pooled, so we rely on finalizer
            // that will be called only if the object is not in the pool.
            // But if we tried to pool the buffer to RMP but failed above
            // then we called GC.SuppressFinalize(this)
            // and finalizer won't be called if the object is dropped from ObjectPool.
            // We have done buffer clean-up job and this object could die normally.
            ObjectPool.Free(this);
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
