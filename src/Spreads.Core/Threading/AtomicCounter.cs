// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Threading
{
    /// <summary>
    /// Counts non-negative value and stores it in provided pointer in pinned/native memory.
    /// </summary>
    [DebuggerDisplay("{" + nameof(Count) + "}")]
    public readonly unsafe struct AtomicCounter
    {
        // We use interlocked operations which are very expensive. Limit the number of bits used
        // for ref count so that other bits could be used for flags. 2^23 should be enough for
        // ref count. Interlocked incr/decr will work with flags without additional logic.

        // All operations must be atomic even if this requires testing value + CAS instead
        // of IL.Incr/Decr. We need correctness first. When justified
        // we could work with the pointer directly.

        public const int CountMask = 0b_00000000_11111111_11111111_11111111;
        public const int CountLimit = CountMask >> 1;

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
            if (pointer == null)
            {
                ThowNullPointerInCtor();
            }

            // prevent disposed and just bad values.
            if ((uint)(*pointer & CountMask) >= CountLimit)
            {
                ThrowBadCounter(*pointer & CountMask);
            }

            Pointer = pointer;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int GetCount(ref int counter)
        {
            return counter & CountMask;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int Increment(ref int counter)
        {
            int newValue;
            while (true)
            {
                // no volatile read, CAS will do the barriers and on next retry it will be ok.
                var currentValue = counter;

                if (unchecked((uint)(currentValue & CountMask)) >= CountLimit)
                {
                    // Counter was negative before increment or there is a counter leak
                    // and we reached 8M values. For any conceivable use case
                    // count should not exceed that number and be even close to it.
                    // In imaginary case when this happens we decrement back before throwing.

                    ThrowBadCounter(currentValue & CountMask);
                }

                newValue = currentValue + 1;
                var existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                if (existing == currentValue)
                {
                    // do not return from while loop
                    break;
                }

                if ((existing & ~CountMask) != (currentValue & ~CountMask))
                {
                    ThrowCounterChanged();
                }
            }
            return newValue & CountMask;
        }

        /// <summary>
        /// Decrement a positive counter. Throws if counter is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int Decrement(ref int counter)
        {
            int newValue;
            while (true)
            {
                // no volatile read, CAS will do the barriers and on next retry it will be ok.
                var currentValue = counter;

                // after decrement the value must remain in the range
                if (unchecked((uint)((currentValue & CountMask) - 1)) >= CountLimit)
                {
                    ThrowBadCounter(currentValue & CountMask);
                }

                newValue = currentValue - 1;
                var existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                if (existing == currentValue)
                {
                    break;
                }

                if ((existing & ~CountMask) != (currentValue & ~CountMask))
                {
                    ThrowCounterChanged();
                }
            }
            return newValue & CountMask;
        }

        /// <summary>
        /// Returns new count value if incremented or zero if the current count value is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int IncrementIfRetained(ref int counter)
        {
            int newValue;
            while (true)
            {
                var currentValue = Volatile.Read(ref counter);
                var currentCount = currentValue & CountMask;
                if (unchecked((uint)(currentCount)) >= CountLimit)
                {
                    ThrowBadCounter(currentValue & CountMask);
                }

                if (currentCount > 0)
                {
                    newValue = currentValue + 1;
                    var existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                    if (existing == currentValue)
                    {
                        break;
                    }

                    if ((existing & ~CountMask) != (currentValue & ~CountMask))
                    {
                        ThrowCounterChanged();
                    }
                }
                else
                {
                    newValue = 0;
                    break;
                }
            }
            return newValue & CountMask;
        }

        /// <summary>
        /// Returns zero if decremented the last reference or current count if it is more than one.
        /// Throws if current count is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int DecrementIfOne(ref int counter)
        {
            int newValue;
            while (true)
            {
                var currentValue = Volatile.Read(ref counter);
                var currentCount = currentValue & CountMask;
                if (unchecked((uint)(currentCount - 1)) >= CountLimit)
                {
                    ThrowBadCounter(currentValue & CountMask);
                }

                if (currentCount == 1)
                {
                    newValue = currentValue - 1;
                    var existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                    if (existing == currentValue)
                    {
                        break;
                    }

                    if ((existing & ~CountMask) != (currentValue & ~CountMask))
                    {
                        ThrowCounterChanged();
                    }
                }
                else
                {
                    newValue = currentValue;
                    break;
                }
            }
            return newValue & CountMask;
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Increment()
        {
            return Increment(ref *Pointer);
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decrement()
        {
            return Decrement(ref *Pointer);
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int IncrementIfRetained()
        {
            return IncrementIfRetained(ref *Pointer);
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int DecrementIfOne()
        {
            return DecrementIfOne(ref *Pointer);
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref *Pointer) & CountMask;
        }

        public int CountOrZero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Pointer == null ? 0 : Volatile.Read(ref *Pointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetIsRetained(ref int counter)
        {
            return unchecked((uint)((counter & CountMask) - 1)) <= CountLimit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetIsDisposed(ref int counter)
        {
            return (counter & CountMask) == CountMask;
        }

        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetIsRetained(ref *Pointer);
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == CountMask;
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Pointer != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static void Dispose(ref int counter)
        {
            var currentValue = Volatile.Read(ref counter);
            var currentCount = currentValue & CountMask;

            if ((uint)(currentValue & CountMask) >= CountLimit)
            {
                ThrowBadCounter(currentValue & CountMask);
            }

            if (currentCount != 0)
            {
                ThrowPositiveRefCount(currentCount);
            }

            var newValue = currentValue | CountMask; // set all count bits to 1, make counter unusable for Incr/Decr methods

            var existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
            if (existing != currentValue)
            {
                ThrowCounterChanged();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TryDispose(ref int counter)
        {
            var currentValue = counter;
            var currentCount = currentValue & CountMask;
            
            if (currentCount != 0)
            {
                return currentCount;
            }

            var newValue = currentValue | CountMask;

            var existing = currentValue; Interlocked.CompareExchange(ref counter, newValue, currentValue);
            if (existing != currentValue)
            {
                return -1;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNonZeroTryDispose(int count)
        {
            if ((uint)(count & CountMask) >= CountLimit)
            {
                ThrowBadCounter(count & CountMask);
            }

            if (count > 0)
            {
                ThrowPositiveRefCount(count);
            }

            if (count == -1)
            {
                ThrowCounterChanged();
            }
            ThrowHelper.FailFast("Wrong value from TryDispose");
        }

        [System.Diagnostics.Contracts.Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Dispose(ref *Pointer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadCounter(int current)
        {
            if (current == CountMask)
            {
                ThrowDisposed();
            }
            else
            {
                ThrowHelper.ThrowInvalidOperationException($"Bad counter: current count {current}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(AtomicCounter));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThowNullPointerInCtor()
        {
            ThrowHelper.ThrowInvalidOperationException($"Pointer in AtomicCounter ctor is null");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowPositiveRefCount(int count)
        {
            ThrowHelper.ThrowInvalidOperationException($"AtomicCounter.Count {count} > 0");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCounterChanged()
        {
            // If this is needed we could easily work directly with pointer.
            ThrowHelper.ThrowInvalidOperationException($"Counter flags changed during operation or count increased during disposal.");
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

        internal readonly int* _pointer;

        private AtomicCounter _poolUsersCounter;

        // TODO (low) OffHeapBuffer Append-only list-like API that never moves existing chunks but adds new if length exceeds current

        public AtomicCounterPool(T pinnedSpan)
        {
            if (pinnedSpan.Length < 4)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(pinnedSpan));
            }
            _pinnedSpan = pinnedSpan;
            _pointer = (int*)pinnedSpan.Data;
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
            get => _pointer;
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
                if (*((int*)_pinnedSpan.Data + ~nextFreeLink) >= 0)
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
                    var _ = _poolUsersCounter.Increment();

                    // *currentFreePointer = 0;
                    existing = Interlocked.CompareExchange(ref *currentFreePointer, 0, nextFreeLink);

                    if (existing == nextFreeLink)
                    {
                        counter = new AtomicCounter(currentFreePointer);
                        // clear content
                        *currentFreePointer = 0;
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
            if (!counter.IsDisposed)
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
        // to the pool from every user.

        // Better to adjust BucketSize so that there are never more than 1 bucket, 4 max for safety so that the service always works.
        // Further increase could be slower when AC owners churn a lot.
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
                if (unchecked((ulong)idx) < (ulong)BucketSize)
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
