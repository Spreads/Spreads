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
using System.Threading;
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
        private static readonly ObjectPool<PrivateMemory<T>> ObjectPool =
            new ObjectPool<PrivateMemory<T>>(() => new PrivateMemory<T>(), Environment.ProcessorCount * 16);

        // In this implementation all blittable (pinnable) types are backed by 
        // native memory (from Marshal.AllocHGlobal/VirtualAlloc/similar)
        // and is always pinned. Therefore there is no need for GCHandle or
        // pinning logic - memory is already pinned when it is possible.
        // We keep track of total number of bytes allocated off-heap per type.
        // We need to support alignment at this level

        // ReSharper disable once StaticMemberInGenericType
        internal static long AllocatedNativeBytes;

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
        internal static unsafe PrivateMemory<T> Create(int length, RetainableMemoryPool<T> pool)
        {
            var alignedSize = (uint) BitUtil.FindNextPositivePowerOfTwo(Unsafe.SizeOf<T>());

            if ((ulong) length * alignedSize > int.MaxValue)
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(length));

            length = Math.Max(length, Settings.MIN_POOLED_BUFFER_LEN);

            var privateMemory = ObjectPool.Allocate();

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
                privateMemory.AllocateBlittable((uint) (length * alignedSize), alignedSize);
                ThrowHelper.DebugAssert(privateMemory._array == null);
            }

            privateMemory.PoolIndex =
                pool is null
                    ? privateMemory._array == null ? (byte) 0 : (byte) 1
                    : pool.PoolIdx;

            return privateMemory;
        }

        private unsafe void AllocateBlittable(uint bytesLength, uint alignment = 0)
        {
            ThrowHelper.DebugAssert(!VecTypeHelper<T>.RuntimeVecInfo.IsReferenceOrContainsReferences);

            if (alignment == 0)
                alignment = (uint) BitUtil.FindNextPositivePowerOfTwo(Unsafe.SizeOf<T>());

            var nm = NativeMemory.Alloc(bytesLength, alignment);

            _pointer = nm.Pointer;
            _offset = nm.AlignmentOffset;
            _length = (int) nm.Length / Unsafe.SizeOf<T>();

            Interlocked.Add(ref AllocatedNativeBytes, bytesLength);
        }

        /// <summary>
        /// Returns <see cref="Vec{T}"/> backed by this instance memory.
        /// </summary>
        public override unsafe Vec<T> Vec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
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
                ThrowDisposed<ArrayMemory<T>>();
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


            ClearAfterDispose();
            if (array != null)
            {
                ThrowHelper.DebugAssert(TypeHelper<T>.IsReferenceOrContainsReferences);
                BufferPool<T>.Return(array, clearArray: true);
            }
            else
            {
                var nativeLength = BitUtil.Align(_length * Unsafe.SizeOf<T>(),
                    BitUtil.FindNextPositivePowerOfTwo(Unsafe.SizeOf<T>()));
                var nm = new NativeMemory((byte*) _pointer, (byte) _offset, (uint) nativeLength);
                nm.Free();
                _pointer = null;
            }

            _offset = -1; // make it unusable if not re-initialized
            PoolIndex = default; // after ExternallyOwned check!


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
            if (IsDisposed)
            {
                ThrowDisposed<ArrayMemory<T>>();
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