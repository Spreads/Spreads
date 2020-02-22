// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Native;
using Spreads.Serialization;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    /// <summary>
    /// Memory owned by the current process (as opposed to shared memory).
    /// </summary>
    /// <remarks>
    /// The memory for blittable types is allocated off-heap, for other types the memory is backed by simple GC-owned arrays.  
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public sealed class PrivateMemory<T> : RetainableMemory<T>
    {
        /// <summary>
        /// Size of PrivateMemory object with header and method table pointer on x64.
        /// </summary>
        internal const int ObjectSize = 48;

        // Size of PrivateMemory object is 48 bytes. It's the main building block
        // for data containers and is often not short-lived, so pool aggressively.
        // Round up to pow2 = 64 bytes, use 16 kb per core per type, which gives 256 items.

        private static readonly ObjectPool<PrivateMemory<T>> ObjectPool =
            new ObjectPool<PrivateMemory<T>>(() => new PrivateMemory<T>(), 16 * 1024 / BitUtil.FindNextPositivePowerOfTwo(ObjectSize));

        // In this implementation all blittable (pinnable) types are backed by 
        // native memory (from Marshal.AllocHGlobal/VirtualAlloc/similar)
        // and is always pinned. Therefore there is no need for GCHandle or
        // pinning logic - memory is already pinned when it is possible.
        // We keep track of total number of bytes allocated off-heap in BuffersStatistics.

        internal T[] _array;
        internal int _offset;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PrivateMemory()
        {
        }

        /// <summary>
        /// Create a <see cref="PrivateMemory{T}"/>.
        /// </summary>
        public static PrivateMemory<T> Create(int length)
        {
            return Create(length, null);
        }

        /// <summary>
        /// Create a <see cref="PrivateMemory{T}"/> from a <see cref="RetainableMemoryPool{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PrivateMemory<T> Create(int length, RetainableMemoryPool<T> pool)
        {
            return Create(length, pool, CpuIdCache.GetCurrentCpuId());
        }

        /// <summary>
        /// Create a <see cref="PrivateMemory{T}"/> from a <see cref="RetainableMemoryPool{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe PrivateMemory<T> Create(int length, RetainableMemoryPool<T> pool, int cpuId)
        {
            var alignedSize = (uint) BitUtil.FindNextPositivePowerOfTwo(Unsafe.SizeOf<T>());

            if ((ulong) length * alignedSize > int.MaxValue)
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));

            length = Math.Max(length, Settings.MIN_POOLED_BUFFER_LEN);

            var privateMemory = ObjectPool.Rent(cpuId);

            // Clear counter (with flags, PM does not have any flags).
            // We cannot tell if ObjectPool allocated a new one or took from pool
            // other then by checking if the counter is disposed, so we cannot require
            // that the counter is disposed. We only need that pooled object has the counter
            // in a disposed state so that no one accidentally uses the object while it is in the pool.
            privateMemory.CounterRef = 0;

            if (TypeHelper<T>.IsReferenceOrContainsReferences)
            {
                privateMemory._array = BufferPool<T>.Rent(length);
                privateMemory._offset = 0;
                ThrowHelper.DebugAssert(privateMemory._offset == 0);
                ThrowHelper.DebugAssert(privateMemory._pointer == null);
            }
            else
            {
                privateMemory.AllocateBlittable((uint) (length * alignedSize), alignedSize, cpuId);
                ThrowHelper.DebugAssert(privateMemory._array == null);
            }

            privateMemory.PoolIndex =
                pool is null
                    ? privateMemory._array == null ? (byte) 0 : (byte) 1
                    : pool.PoolIdx;

            return privateMemory;
        }

        private unsafe void AllocateBlittable(uint bytesLength, uint alignment, int cpuId)
        {
            if (!NativeAllocatorSettings.Initialized) ThrowHelper.ThrowInvalidOperationException();

            ThrowHelper.DebugAssert(!VecTypeHelper<T>.RuntimeVecInfo.IsReferenceOrContainsReferences);

            ThrowHelper.DebugAssert(BitUtil.IsPowerOfTwo((int) alignment));

            // It doesn't make a lot of sense to have it above a cache line (or 64 bytes for AVX512).
            // But cache line could be 128 (already exists) and CUDA could have 256 bytes alignment.
            // Three 64, 128 or 256
            alignment = Math.Min(Math.Max(Settings.AVX512_ALIGNMENT, alignment), Settings.SAFE_CACHE_LINE * 2);

            // TODO bytesLength = (uint) Mem.GoodSize((UIntPtr) bytesLength); but check/change return type 
            
            _pointer = Mem.MallocAligned((UIntPtr) bytesLength, (UIntPtr) alignment); 
            _offset = 0;
            _length = (int) bytesLength / Unsafe.SizeOf<T>();

            BuffersStatistics.AllocatedNativeMemory.InterlockedAdd(bytesLength, cpuId);
        }

        /// <summary>
        /// Returns <see cref="Vec{T}"/> backed by this instance memory.
        /// </summary>
        public override unsafe Vec<T> Vec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO use static readonly for blittables + debug assert that for them pointer is always != null
                // var tid = VecTypeHelper<T>.RuntimeVecInfo.RuntimeTypeId;
                var vec = _pointer == null ? new Vec<T>(_array, _offset, _length) : new Vec<T>(_pointer, _length);
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
                ThrowNotPinnable();
            }

            return base.Pin(elementIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotPinnable()
        {
            ThrowHelper.ThrowInvalidOperationException($"Type {typeof(T).Name} is not pinnable.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe Span<T> GetSpan()
        {
            if (IsPooled)
            {
                ThrowDisposed<PrivateMemory<T>>();
            }

            // if disposed Pointer & _len are null/0, no way to corrupt data, will just throw
            if (_pointer == null)
            {
                return new Span<T>(_array, _offset, _length);
            }

            return new Span<T>(_pointer, _length);
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
                ThrowHelper.DebugAssert(TypeHelper<T>.IsReferenceOrContainsReferences);
                BufferPool<T>.Return(array, clearArray: true);
            }
            else
            {
                Mem.Free((byte*) _pointer);
                BuffersStatistics.AllocatedNativeMemory.InterlockedAdd(-_length);
                _pointer = null;
            }

            ClearAfterDispose();

            _offset = -1; // make it unusable if not re-initialized
            PoolIndex = default; // after ExternallyOwned check!

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
            if (IsDisposed)
            {
                ThrowDisposed<PrivateMemory<T>>();
            }

            if (_array == null)
            {
                buffer = default;
                return false;
            }

            buffer = new ArraySegment<T>(_array, _offset, _length);
            return true;
        }
    }
}