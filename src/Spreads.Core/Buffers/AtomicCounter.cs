// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Buffers
{
    // TODO Scan disposed but not released counters (value == -1)
    // However we should rather guarantee release in finalizer even
    // when counter > 0 if the object being finalized is exclusive owner.
    // Owners should be pooled and such events should be quite rare.
    // TODO logging and/or performanc counters for AC Acquire/Release.

    /// <summary>
    /// Counts non-negative value and stores it in provided pointer in pinned/native memory.
    /// </summary>
    public readonly unsafe struct AtomicCounter
    {
        /// <summary>
        /// Value in pointer:
        /// 0 - not retained, could be taken from pool for temp usage with e.g. return in finally
        /// > 0 - number of retained calls
        /// -1 - disposed, either to object pool or GC
        /// less than -1 - ~(value) is index of the next free slot in the pool
        /// </summary>
        internal readonly int* Pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AtomicCounter(int* pointer)
        {
            int value = Volatile.Read(ref *pointer);
            if (value != 0)
            {
                ThowNonZeroRefCountInCtor(value);
            }
            Pointer = pointer;
        }

        // TODO wrap in AdditionalCorrectnessCheck when stable. Probably owners already check IsDisposed on every call.

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Increment()
        {
            var value = Interlocked.Increment(ref *Pointer);
            // check after is 170 MOPS vs 130 MOPS check disposed before increment
            if (value <= 0)
            {
                // TODO we could corrupt free list, need to recover. -1 indicates disposed and points to free list
                var existing = Interlocked.CompareExchange(ref *Pointer, -1, 0);
                if (existing != 0)
                {
                    // int overflow will be there, if reached int.Max then definitely fail fast
                    IncrementFailReleased();
                }
                // Was disposed but not released. Should be recoverable and at least allow to owner finalizer to clean AC.
                *Pointer = -1;
                ThrowDisposed();
            }
            return value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void IncrementFailReleased()
        {
            ThrowHelper.FailFast("Incrementing released or renewed AtomicCounter, possibly corrupted free list");
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decrement()
        {
            var value = Interlocked.Decrement(ref *Pointer);
            if (value < 0)
            {
                // now in disposed state
                ThowNegativeRefCount(Count);
            }
            return value;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref *Pointer);
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count < 0;
        }

        public bool IsReleased
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count < -1;
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Pointer != null;
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var existing = Interlocked.CompareExchange(ref *Pointer, -1, 0);
            if (existing > 0)
            {
                ThowPositiveRefCount(Count);
            }
            if (existing < 0)
            {
                ThrowDisposed();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(AtomicCounter));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThowNonZeroRefCountInCtor(int count)
        {
            ThrowHelper.ThrowInvalidOperationException($"Pointer in AtomicCounter.Count ctor has non-zero value of {count}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThowNegativeRefCount(int count)
        {
            ThrowHelper.ThrowInvalidOperationException($"AtomicCounter.Count {count} < 0");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThowPositiveRefCount(int count)
        {
            ThrowHelper.ThrowInvalidOperationException($"AtomicCounter.Count {count} > 0");
        }
    }

    /// <summary>
    /// Pool of `AtomicCounter`s backed by native memory. Avoid long-living pinned buffer in managed heap.
    /// </summary>
    internal unsafe class AtomicCounterPool<T> : IDisposable where T : IPinnedSpan<int>
    {
        /// <summary>
        /// Internal for tests only, do not use
        /// </summary>
        internal readonly T _pinnedSpan;

        private AtomicCounter _poolUsersCounter;

        // TODO (low) OffHeapBuffer Append-only list-like API that never moves existing chunks but adds new if length exceeds current

        public AtomicCounterPool(T pinnedSpan)
        {
            if (pinnedSpan.Length < 4)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(pinnedSpan));
            }
            _pinnedSpan = pinnedSpan;
            Init();
        }

        internal void Init()
        {
            // we cannot acquire _poolUsersCounter via TryAcquireCounter because that method uses _poolUsersCounter
            var poolCounterPtr = (int*)_pinnedSpan.Data + 1;
            *poolCounterPtr = 0;
            _poolUsersCounter = new AtomicCounter(poolCounterPtr);
            var _ = _poolUsersCounter.Increment();

            var len = _pinnedSpan.Length;
            for (int i = 2; i < len - 1; i++)
            {
                // ReSharper disable once PossibleStructMemberModificationOfNonVariableStruct
                _pinnedSpan[i] = ~(i + 1);
            }

            // ReSharper disable once PossibleStructMemberModificationOfNonVariableStruct
            _pinnedSpan[0] = ~2;

            // ReSharper disable once PossibleStructMemberModificationOfNonVariableStruct
            _pinnedSpan[len - 1] = ~0;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pinnedSpan.Length - 2;
        }

        public int FreeCount
        {
            // TODO free counter is very expensive, consider removing it
            // Current vision is that AC is owned by a pooled object so
            // the cost is amortized. Already quite fast:
            // 23 MOPS with free count vs 30 MOPS without.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pinnedSpan.Length - _poolUsersCounter.Count - 1;
        }

        internal int* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int*)_pinnedSpan.Data;
        }

        /// <summary>
        /// Return a new AtomicCounter backed by some slot in the pool.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquireCounter(out AtomicCounter counter)
        {
            var spinner = new SpinWait();
            while (true)
            {
                var currentFreeLink = *(int*)_pinnedSpan.Data;
                var currentFreeIdx = ~currentFreeLink;
                if (currentFreeIdx == 0)
                {
                    Debug.Assert(FreeCount == 0);
                    counter = default;
                    return false;
                }

                if (currentFreeIdx == 1)
                {
                    TryAcquireCounterFailWrongImpl();
                }
                Debug.Assert(currentFreeIdx > 1);

                // R# is stupid, without outer parenths the value will be completely different
                // ReSharper disable once ArrangeRedundantParentheses
                var currentFreePointer = (int*)_pinnedSpan.Data + currentFreeIdx;

                var nextFreeLink = *currentFreePointer;

                // ensure that the next free link is free
                
                // ReSharper disable once ArrangeRedundantParentheses
                if (*((int*) _pinnedSpan.Data + ~nextFreeLink) >= 0)
                {
                    // The thing we want to put to the free list top is not free.
                    // This is only possible if Increment() was called on released AC,
                    // but we FailFast there if could detect. But logic there is not
                    // tested yet in stress situations so FF there as well.
                    // Anyway this is badly broken user code so do not try any recovery.
                    TryAcquireCounterFreeListIsBroken();
                }

                var existing = Interlocked.CompareExchange(ref *(int*)_pinnedSpan.Data, nextFreeLink, currentFreeLink);

                if (existing == currentFreeLink)
                {
                    // var _ = _poolUsersCounter.Increment();

                    // *currentFreePointer = 0;
                    existing = Interlocked.CompareExchange(ref *currentFreePointer, 0, nextFreeLink);

                    if (existing == nextFreeLink)
                    {
                        counter = new AtomicCounter(currentFreePointer);
                        return true;
                    }
                }
                spinner.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryAcquireCounterFailWrongImpl()
        {
            ThrowHelper.FailFast("Wrong implementation of TryAcquireCounter: free idx points to inner counter");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryAcquireCounterFreeListIsBroken()
        {
            ThrowHelper.FailFast("Free list is broken");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseCounter(AtomicCounter counter)
        {
            if (counter.Count != -1)
            {
                ReleaseCounterFailNotDisposed();
            }
            var p = (void*)counter.Pointer;
            var idx = checked((int)(((byte*)p - (byte*)_pinnedSpan.Data) >> 2)); // divide by 4
            if (idx < 1 || idx >= _pinnedSpan.Length)
            {
                ReleaseCounterFailNotFromPool();
            }
            var spinner = new SpinWait();
            while (true)
            {
                var currentFreeLink = *(int*)_pinnedSpan.Data;
                var thisFreePointer = (int*)_pinnedSpan.Data + idx;
                *thisFreePointer = currentFreeLink;
                var existing = Interlocked.CompareExchange(ref *(int*)_pinnedSpan.Data, ~idx, currentFreeLink);
                if (existing == currentFreeLink)
                {
                    var _ = _poolUsersCounter.Decrement();
                    return;
                }
                spinner.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReleaseCounterFailNotDisposed()
        {
            ThrowHelper.ThrowInvalidOperationException("Counter must be disposed before release.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReleaseCounterFailNotFromPool()
        {
            ThrowHelper.ThrowInvalidOperationException($"Counter is not from pool");
        }

        //internal void ScanDroppped()
        //{
        //    for (int i = 0; i < _pinnedSpan.Length; i++)
        //    {
        //        // TODO
        //    }
        //}

        public void Dispose()
        {
            var remaining = _poolUsersCounter.Decrement();
            if (remaining == 0)
            {
                _poolUsersCounter.Dispose();
                _pinnedSpan.Dispose();
            }
            else if (remaining > 0)
            {
                ThrowHelper.ThrowInvalidOperationException($"Cannot dipose AtomicCounterPool: there are {_poolUsersCounter} outstanding counters.");
            }
            else
            {
                ThrowHelper.ThrowObjectDisposedException("AtomicCounterPool");
            }
            GC.SuppressFinalize(this);
        }

        ~AtomicCounterPool()
        {
            Dispose();
        }
    }

    public static unsafe class AtomicCounterService
    {
        // NOTE: Service as static class not instance because any users of AC
        // will benefit from reusing slots in ACP and we save 8 byte field reference
        // to the pool from every user (yet AC will have it TODO but we can find bucket by Ptr). We could find required bucket by pointer / length

        // Better to adjust BucketSize so that there are never than 1 bucket, 4 max for safety so that the service always works.
        // Further increase could be slower.
        internal static readonly int BucketSize = BitUtil.FindNextPositivePowerOfTwo(Math.Max(3, Settings.AtomicCounterPoolBucketSize));

        // By trying it first we save array deref and null check. This is not created until first usage of the service.
        internal static readonly AtomicCounterPool<OffHeapBuffer<int>> FirstBucket =
            new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(BucketSize));

        // Array object is Pow2 and less than 64 bytes with header.
        internal static AtomicCounterPool<OffHeapBuffer<int>>[] Buckets = { FirstBucket, null, null, null };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AtomicCounter AcquireCounter()
        {
            if (TryAcquireCounter(out var counter))
            {
                return counter;
            }
            return AcquireCounterSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static AtomicCounter AcquireCounterSlow()
        {
            lock (Buckets)
            {
                if (TryAcquireCounter(out var counter))
                {
                    return counter;
                }

                for (int i = 0; i < Buckets.Length; i++)
                {
                    var pool = Buckets[i];
                    if (pool == null)
                    {
                        Buckets[i] = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(BucketSize));
                        if (TryAcquireCounter(out counter))
                        {
                            return counter;
                        }
                        ThrowHelper.FailFast("WTF, we just allocated new ACP bucket.");
                    }
                }

                var newBucketsArray = new AtomicCounterPool<OffHeapBuffer<int>>[Buckets.Length * 2];
                Buckets.CopyTo(newBucketsArray, 0);
                Buckets = newBucketsArray;
                // Lock re-entry and retry: first retry attempt must succeed by design, we are still in the lock and producing space for ourselves.
                return AcquireCounterSlow();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryAcquireCounter(out AtomicCounter counter)
        {
            if (FirstBucket.TryAcquireCounter(out counter))
            {
                return true;
            }

            for (var i = 1; i < Buckets.Length; i++)
            {
                var bucket = Buckets[i];
                if (bucket == null)
                {
                    break;
                }

                if (bucket.TryAcquireCounter(out counter))
                {
                    return true;
                }
            }

            counter = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReleaseCounter(AtomicCounter counter)
        {
            foreach (var bucket in Buckets)
            {
                // linear search
                var p = counter.Pointer;
                var idx = p - bucket.Pointer;
                if ((ulong)idx < (ulong)BucketSize)
                {
                    bucket.ReleaseCounter(counter);
                    return;
                }
            }

            ThrowAlienCounter();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowAlienCounter()
        {
            ThrowHelper.ThrowInvalidOperationException("Counter was not acquired from AtomicCounterService");
        }
    }
}
