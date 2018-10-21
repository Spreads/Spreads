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

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Increment()
        {
            return Interlocked.Increment(ref *Pointer);
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decrement()
        {
            var value = Interlocked.Increment(ref *Pointer);
            if (value < 0)
            {
                // now in disposed state
                ThowNegativeRefCount();
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
            get => Count < -1;
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var existing = Interlocked.CompareExchange(ref *Pointer, -1, 0);
            if (existing > 0)
            {
                ThowPositiveRefCount();
            }
            if (existing == -1)
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
        private void ThowNegativeRefCount()
        {
            ThrowHelper.ThrowInvalidOperationException($"AtomicCounter.Count {Count} < 0");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThowPositiveRefCount()
        {
            ThrowHelper.ThrowInvalidOperationException($"AtomicCounter.Count {Count} > 0");
        }
    }

    /// <summary>
    /// Pool of `AtomicCounter`s backed by native memory. Avoid long-living pinned buffer in managed heap.
    /// </summary>
    internal unsafe class AtomicCounterPool<T> : IDisposable where T : IPinnedSpan<int>
    {
        private readonly T _pinnedSpan;

        private AtomicCounter _poolUsersCounter;

        // TODO (low) OffHeapBuffer Append-only list-like API that never moves existing chunks but adds new if length exceeds current

        public AtomicCounterPool(T pinnedSpan)
        {
            if (pinnedSpan.Length < 4)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(pinnedSpan));
            }
            _pinnedSpan = pinnedSpan;
            _pinnedSpan.Span.Slice(1).Fill(-1);
            //length = BitUtil.FindNextPositivePowerOfTwo(length);
            //OffHeapBuffer<int> buffer = new OffHeapBuffer<int>(length);
            Init();
            ThrowHelper.AssertFailFast(TryAcquireCounter(out _poolUsersCounter), "First internal counter must always be acquired");
            // this object is one of the users, it must decrement  the counter on dispose
            var _ = _poolUsersCounter.Increment();
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pinnedSpan.Length - 2;
        }

        public int FreeCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pinnedSpan.Length - _poolUsersCounter.Count - 1;
        }

        internal int* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int*)_pinnedSpan.Data;
        }

        internal void Init()
        {
            var len = _pinnedSpan.Length;
            for (int i = 0; i < len - 1; i++)
            {
                ref var x = ref _pinnedSpan[i];
                // 0 |> -2 |> 1 - first free is at index 1
                // 1 |> -3 |> 2 - 1st free points to idx 2, etc.
                x = -(i + 1);
            }
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
                Debug.Assert(currentFreeIdx > 0);

                // R# is stupid, without outer parenths the value will be completely different
                // ReSharper disable once ArrangeRedundantParentheses
                var currentFreePointer = (int*)_pinnedSpan.Data + currentFreeIdx;
                var nextFreeLink = *currentFreePointer;
                var existing = Interlocked.CompareExchange(ref *(int*)_pinnedSpan.Data, nextFreeLink, currentFreeLink);
                if (existing == currentFreeLink)
                {
                    var _ = _poolUsersCounter.Increment();
                    *currentFreePointer = 0;
                    counter = new AtomicCounter(currentFreePointer);
                    return true;
                }
                spinner.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReleaseCounter(AtomicCounter counter)
        {
            if (!counter.IsDisposed)
            {
                ThrowHelper.ThrowInvalidOperationException("Counter must be disposed before release.");
            }
            var p = (void*)counter.Pointer;
            var idx = checked((int)(((byte*)p - (byte*)_pinnedSpan.Data) >> 2)); // divide by 4
            if (idx < 1 || idx >= _pinnedSpan.Length)
            {
                ThrowHelper.ThrowInvalidOperationException("Counter is not from this pool");
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

        internal void ScanDroppped()
        {
            for (int i = 0; i < _pinnedSpan.Length; i++)
            {
                // TODO
            }
        }

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
                // ACP should be a field of an object that has AC as a field
                // That object must be pooled and keep the AC in disposed state when pooled
                // and dispose AC in its Dispose method. Then ACP could live only via references
                // from pooled objects and will be released eventually via finalizer.
                Trace.TraceWarning($"Cannot dipose AtomicCounterPool: there are {_poolUsersCounter} outstanding counters.");
            }
            else
            {
                ThrowHelper.ThrowObjectDisposedException("AtomicCounterPool");
            }
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
        internal static readonly int BucketSize = BitUtil.FindNextPositivePowerOfTwo(Settings.AtomicCounterPoolBucketSize);

        // By trying it first we save array deref and null check.
        internal static readonly AtomicCounterPool<OffHeapBuffer<int>> FirstBucket =
            new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(BucketSize));

        // Array object is Pow2 and less than 64 bytes with header.
        internal static AtomicCounterPool<OffHeapBuffer<int>>[] Buckets =
            new AtomicCounterPool<OffHeapBuffer<int>>[4] { FirstBucket, null, null, null };

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
                if ((ulong) idx < (ulong) BucketSize)
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
