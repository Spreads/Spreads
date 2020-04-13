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
    /// <summary>
    /// Memory owned by the current process (as opposed to shared memory).
    /// </summary>
    /// <remarks>
    /// The memory for blittable types is allocated off-heap, for other types the memory is backed by GC-owned arrays.
    /// Blittable types are defined as types for which <see cref="RuntimeHelpers.IsReferenceOrContainsReferences{T}"/>
    /// is false.
    ///
    /// <para />
    ///
    /// It's possible to use <see cref="Vec.UnsafeGetRef{T}"/> and other unsafe-prefixed method of <see cref="Vec"/>
    /// returned from <see cref="PrivateMemory{T}.GetVec"/>.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public sealed class PrivateMemory<T> : RetainableMemory<T>
    {
#pragma warning disable 169
        /// <summary>
        /// Without padding there is false sharing on <see cref="RefCountedMemory{T}.CounterRef"/>
        /// that is modified during pool rent/return. We use <see cref="Vec"/> as a padding as
        /// well, because it is unused during rent/return and when it's used the memory remains rented.
        /// </summary>
        private readonly Padding24 _padding;
#pragma warning restore 169

        /// <summary>
        /// Size of PrivateMemory object with header and method table pointer on x64.
        /// </summary>
        internal const int ObjectSize = 88;

        // Size of PrivateMemory object is 48 bytes. It's the main building block
        // for data containers and is often not short-lived, so pool aggressively.
        // Round up to pow2, use 16 kb per core per type.

        private const int PerCorePoolSize = 16 * 1024 / 128; // BitUtil.FindNextPositivePowerOfTwo(ObjectSize);

        internal static readonly ObjectPool<PrivateMemory<T>> ObjectPool = CreateObjectPool();

        private static ObjectPool<PrivateMemory<T>> CreateObjectPool()
        {
            var op = new ObjectPool<PrivateMemory<T>>(() =>
            {
                var pm = new PrivateMemory<T>();
                AtomicCounter.Dispose(ref pm.CounterRef);
                return pm;
            }, Settings.PrivateMemoryPerCorePoolSize ?? PerCorePoolSize);

            // Need to touch these fields very early in a common not hot place for JIT static
            // readonly optimization even if tiered compilation is off.
            // Note single & to avoid short circuit.
            if (AdditionalCorrectnessChecks.Enabled & TypeHelper<T>.IsReferenceOrContainsReferences) 
                ThrowHelper.Assert(!op.IsDisposed);
            
            return op;
        }

        // In this implementation all blittable (pinnable) types are backed by 
        // native memory (from Marshal.AllocHGlobal/VirtualAlloc/similar)
        // and is always pinned. Therefore there is no need for GCHandle or
        // pinning logic - memory is already pinned when it is possible.
        // We keep track of total number of bytes allocated off-heap in BuffersStatistics.

        private PrivateMemory()
        {
            IsBlittableOffheap = true;
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
        internal static PrivateMemory<T> Create(int length, RetainableMemoryPool<T>? pool)
        {
            length = Math.Max(length, Settings.MIN_POOLED_BUFFER_LEN);

            var cpuId = Cpu.GetCurrentCoreId();

            var privateMemory = ObjectPool.Rent(cpuId);
            ThrowHelper.DebugAssert(privateMemory.IsDisposed);

            // Clear counter (with flags, PM does not have any flags).
            // We cannot tell if ObjectPool allocated a new one or took from pool
            // other then by checking if the counter is disposed, so we cannot require
            // that the counter is disposed. We only need that pooled object has the counter
            // in a disposed state so that no one accidentally uses the object while it is in the pool.
            privateMemory.CounterRef = 0;

            if (TypeHelper<T>.IsReferenceOrContainsReferences)
            {
                var arr = BufferPool<T>.Rent(length);
                privateMemory._array = arr;
                privateMemory._length = arr.Length;
                privateMemory._offset = 0;
                ThrowHelper.DebugAssert(privateMemory._pointer == default);
            }
            else
            {
                privateMemory.AllocateBlittable(length, cpuId);
                ThrowHelper.DebugAssert(privateMemory._array == null);
            }

            privateMemory.PoolIndex =
                pool is null
                    ? (byte) 1
                    : pool.PoolIdx;

            privateMemory._memory = privateMemory.CreateMemory();

            return privateMemory;
        }

        private unsafe void AllocateBlittable(int length, int cpuId)
        {
            if (!NativeAllocatorSettings.Initialized)
                ThrowHelper.ThrowInvalidOperationException();

            // It doesn't make a lot of sense to have it above a cache line (or 64 bytes for AVX512).
            // But cache line could be 128 (already exists) and CUDA could have 256 bytes alignment.
            // Three possible values: 64, 128 or 256
            var alignment = Math.Min(Math.Max(Settings.AVX512_ALIGNMENT, BitUtil.FindNextPositivePowerOfTwo(Unsafe.SizeOf<T>())),
                Settings.SAFE_CACHE_LINE * 2);

            var bytesLength = (long) length * Unsafe.SizeOf<T>();

            // TODO this is somewhat reasonable, but need to come back here after doing work on DataBlock
            // Do we need Pow2 there? Or we are doing only binary search?
            // Is RingBuffer implementation benefiting from pow2 or it's marginal?
            // Ideally we must use the same native buffer size for any T. 

            if (bytesLength < alignment)
            {
                // MIN_POOLED_BUFFER_LEN is minimum buffer size in bytes for byte type.
                alignment = Math.Min(BitUtil.FindNextPositivePowerOfTwo(Unsafe.SizeOf<T>()), Settings.MIN_POOLED_BUFFER_LEN);
            }

            bytesLength = (long) Mem.GoodSize((UIntPtr) BitUtil.FindNextPositivePowerOfTwo(bytesLength));

            while (true)
            {
                var c = 0;
                try
                {
                    // TODO
                    //_pointer = (IntPtr) Mem.MallocAligned((UIntPtr) bytesLength, (UIntPtr) alignment);
                    _pointer = Marshal.AllocHGlobal((IntPtr) bytesLength);
                    break;
                }
                catch (OutOfMemoryException)
                {
                    c++;
                    GC.Collect(Math.Max(c, 2), c <= 2 ? GCCollectionMode.Default : GCCollectionMode.Forced, c >= 4, c >= 5);
                    if (c > 5)
                        throw;
                }
            }

            _offset = 0;
            _length = (int) bytesLength / Unsafe.SizeOf<T>();

            // Note that we do not call GC.AddMemoryPressure(bytesLength)
            // because the entire point of using native memory is to avoid GC.
            // When system available memory becomes low GC will become more 
            // aggressive and will finalize not disposed memory.
            // But with correct usage there should be no not disposed 
            // memory, so we should not bother for incorrect usage.
            // And even in the later case, an app should survive without OOM (TODO test)
            BuffersStatistics.AllocatedNativeMemory.InterlockedAdd(bytesLength, cpuId);
        }

        /// <summary>
        /// Returns <see cref="Vec{T}"/> backed by this instance memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe Vec<T> GetVec()
        {
            if (IsDisposed)
                ThrowDisposed<PrivateMemory<T>>();

            var vec = TypeHelper<T>.IsReferenceOrContainsReferences
                ? new Vec<T>(Unsafe.As<T[]>(_array), _offset, _length)
                : new Vec<T>((void*) _pointer, _length);

#if SPREADS
            ThrowHelper.DebugAssert(vec.AsVec().ItemType == typeof(T));
#endif
            return vec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe Span<T> GetSpan()
        {
            // Do not use IsDisposed, we use Span.Clear in RMP.RI
            if (TypeHelper<T>.IsReferenceOrContainsReferences ? _array == null : _pointer == IntPtr.Zero)
                ThrowDisposed<PrivateMemory<T>>();

            return TypeHelper<T>.IsReferenceOrContainsReferences
                ? new Span<T>(Unsafe.As<T[]>(_array), _offset, _length)
                : new Span<T>((void*) _pointer, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed)
                ThrowDisposed<PrivateMemory<T>>();

            // With pooling it's quite problematic to expose the internal
            // array that could be used somewhere while being returned to 
            // a pool. It's safe to not even try. Pooled arrays are 
            // more like native memory which is managed manually, so do
            // not return true even when backed by arrays.
            buffer = default;
            return false;
        }

        internal override unsafe void Free(bool finalizing)
        {
            ThrowHelper.Assert(IsDisposed);

            var array = Interlocked.Exchange(ref _array, null);
            if (array != null)
            {
                ThrowHelper.DebugAssert(TypeHelper<T>.IsReferenceOrContainsReferences);
                if(!finalizing && !ExternallyOwned)
                    BufferPool<T>.Return(Unsafe.As<T[]>(array), clearArray: true);
            }

            var pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
            if (pointer != IntPtr.Zero)
            {
                // TODO
                Marshal.FreeHGlobal(pointer);
                //Mem.Free((byte*) pointer);
                BuffersStatistics.ReleasedNativeMemory.InterlockedAdd(_length);
            }

            // In PM either pointer or array, never both
            if (array == null && pointer == IntPtr.Zero)
            {
                string msg = "Tried to free already freed PrivateMemory";
#if DEBUG
                ThrowHelper.ThrowInvalidOperationException(msg);
#endif
                Trace.TraceWarning(msg);
                return;
            }

            ClearFields();

            // We cannot tell if this object is pooled, so we rely on finalizer
            // that will be called only if the object is not in the pool.
            // But if we tried to pool the buffer to RMP but failed above
            // then we called GC.SuppressFinalize(this)
            // and finalizer won't be called if the object is dropped from ObjectPool.
            // We have done buffer clean-up job and this object could die normally.
            if (!finalizing)
                ObjectPool.Return(this);
        }
    }
}