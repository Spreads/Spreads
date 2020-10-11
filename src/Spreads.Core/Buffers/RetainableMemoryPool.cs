// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.CompilerServices;
using Spreads.Collections.Concurrent;
using Spreads.Native;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    /// <summary>
    /// A custom <see cref="MemoryPool{T}"/> implementation.
    /// </summary>
    public class RetainableMemoryPool<T> : MemoryPool<T>
    {
        internal static readonly RetainableMemoryPool<T>?[] KnownPools = new RetainableMemoryPool<T>[64];

        internal static readonly Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> DefaultFactory = (pool, length) => PrivateMemory<T>.Create(length, pool);
        
        public static RetainableMemoryPool<T> Default = new RetainableMemoryPool<T>(DefaultFactory);
        
        public readonly byte PoolIdx;

        /// <summary>
        /// Set to true to always clean on return and clean buffers produced by the factory provided to the constructor.
        /// </summary>
        public readonly bool IsRentAlwaysClean;

        public readonly int MinBufferLength;
        public readonly int MaxBufferLength;
        public readonly int MaxBucketsToTry;

        private readonly bool _typeHasReferences = TypeHelper<T>.IsReferenceOrContainsReferences;

        internal readonly Func<int, RetainableMemory<T>> Factory;

        /// <summary>The default minimum length of each memory buffer in the pool.</summary>
        public const int DefaultMinBufferLength = 16;

        /// <summary>The default maximum length of each memory buffer in the pool (2^20).</summary>
        public const int DefaultMaxBufferLength = 1024 * 1024;

        /// <summary>The default maximum number of memory buffers per bucket per core that are available for rent.</summary>
        public const int DefaultMaxNumberOfBuffersPerBucketPerCore = 8;
        
        public const int DefaultMaxBucketsToTry = 2;

        private readonly MemoryBucket[] _buckets;

        private readonly int _minBufferLengthPow2;
        internal volatile bool _disposed;

        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        internal bool AddStackTraceOnRent = LeaksDetection.Enabled;

        public RetainableMemoryPool(Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> factory,
            int minLength = DefaultMinBufferLength,
            int maxLength = DefaultMaxBufferLength,
            int maxBuffersPerBucketPerCore = DefaultMaxNumberOfBuffersPerBucketPerCore,
            int maxBucketsToTry = DefaultMaxBucketsToTry,
            bool rentAlwaysClean = false)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (KnownPools)
            {
                // Start from 2, other code depends on the first 2 items being null.
                // pool idx == 0 is always null which means a buffer is not from pool
                // pool idx == 1 means a buffer is from default pool, e.g. static array pool
                for (int i = 2; i < KnownPools.Length; i++)
                {
                    if (KnownPools[i] == null)
                    {
                        PoolIdx = checked((byte) i);
                        KnownPools[i] = this;
                        break;
                    }
                }

                if (PoolIdx == 0)
                    ThrowHelper.ThrowInvalidOperationException("KnownPools slots exhausted. 64 pools ought to be enough for anybody.");
            }

            IsRentAlwaysClean = rentAlwaysClean;

            Factory = (length) =>
            {
                var memory = factory(this, length);
                if (IsRentAlwaysClean)
                    memory.GetSpan().Clear();
                AtomicCounter.Dispose(ref memory.CounterRef);
                ThrowHelper.DebugAssert(memory.IsPooled);
                return memory;
            };

            if (minLength <= 16)
            {
                minLength = 16;
            }

            _minBufferLengthPow2 = 32 - BitUtils.LeadingZeroCount(minLength - 1);
            MinBufferLength = 1 << _minBufferLengthPow2;

            if (maxBucketsToTry < 0)
            {
                maxBucketsToTry = 0;
            }

            MaxBucketsToTry = maxBucketsToTry;

            if (maxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            }

            if (maxBuffersPerBucketPerCore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBuffersPerBucketPerCore));
            }

            // Our bucketing algorithm has a min length of 2^4 and a max length of 2^30.
            // Constrain the actual max used to those values.
            const int maximumBufferLength = 0x40000000;

            if (maxLength > maximumBufferLength)
            {
                maxLength = maximumBufferLength;
            }
            else if (maxLength < minLength)
            {
                maxLength = minLength;
            }

            MaxBufferLength = maxLength;

            // Create the buckets.
            int maxBuckets = SelectBucketIndex(maxLength);
            var buckets = new MemoryBucket[maxBuckets + 1];

            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new MemoryBucket(this, GetMaxSizeForBucket(i), maxBuffersPerBucketPerCore);
            }

            _buckets = buckets;
        }

        /// <summary>
        /// Gets an ID for the pool to use with events.
        /// </summary>
        /// <remarks>
        /// <see cref="PoolIdx"/> is per type.
        /// </remarks>
        private int Id => GetHashCode();

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            return RentMemory(minBufferSize, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="minBufferSize"></param>
        /// <param name="exactBucket">If true then <see cref="MaxBucketsToTry"/> value from ctor is ignored and only a single bucket is tried before allocating a new buffer.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainableMemory<T> RentMemory(int minBufferSize, bool exactBucket = false)
        {
            var maxBucketsToTry = exactBucket ? 0 : MaxBucketsToTry;

            var cpuId = Cpu.GetCurrentCoreId();

            minBufferSize = Math.Max(MinBufferLength, minBufferSize);

            var log = RetainableMemoryPoolEventSource.Log;
            RetainableMemory<T> memory;

            int bucketIndex = SelectBucketIndex(minBufferSize);

            if (bucketIndex < _buckets.Length)
            {
                int i = bucketIndex;

                // Search for a buffer starting at the 'index' bucket. If the bucket is empty, bump up to the
                // next higher bucket and try that one, but only try at most a few buckets.
                do
                {
                    memory = _buckets[i].Rent(cpuId);
                } while (memory == null
                         && ++i < _buckets.Length
                         && i <= bucketIndex + maxBucketsToTry);

                if (memory == null)
                {
                    // The pool was exhausted for this buffer size. Allocate a new buffer
                    // with a size corresponding to the appropriate bucket.
                    memory = _buckets[bucketIndex].CreateNew();
                }
                else if (log.IsEnabled())
                {
                    log.BufferRented(memory.GetHashCode(), memory.Length, Id, _buckets[i].GetHashCode());
                }
                
#if DEBUG
                if (AddStackTraceOnRent)
                    memory.Tag = Environment.StackTrace;
#endif
            }
            else
            {
                // The request was for a size too large for the pool. Allocate a buffer of exactly the requested length.
                // When it's returned to the pool, we'll simply throw it away.
                memory = CreateNew(minBufferSize);
            }

            ThrowHelper.DebugAssert(memory.IsDisposed && memory.PoolIndex == PoolIdx, "buffer.IsDisposed && buffer.PoolIndex == PoolIdx");

            // Set counter to zero, keep other flags
            // Do not need atomic CAS here because we own the buffer here
            memory.CounterRef &= ~AtomicCounter.CountMask;
            
            ThrowHelper.DebugAssert(!memory.IsDisposed && memory.ReferenceCount == 0, "!buffer.IsDisposed");
            
            if (log.IsEnabled())
            {
                int bufferId = memory.GetHashCode(), bucketId = -1; // no bucket for an on-demand allocated buffer
                log.BufferRented(bufferId, memory.Length, Id, bucketId);
                log.BufferAllocated(bufferId, memory.Length, Id, bucketId,
                    bucketIndex >= _buckets.Length
                        ? RetainableMemoryPoolEventSource.BufferAllocatedReason.OverMaximumSize
                        : RetainableMemoryPoolEventSource.BufferAllocatedReason.PoolExhausted);
            }
#if DEBUG
            if (AddStackTraceOnRent)
            {
                memory.Tag = Environment.StackTrace;
            }
#endif
            return memory;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private RetainableMemory<T> CreateNew(int minBufferSize)
        {
            return Factory(minBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal bool ReturnInternal(RetainableMemory<T> memory, bool clearMemory = true)
        {
            if (_disposed)
                return false;

            if (!memory.IsDisposed)
                ThrowHelper.ThrowInvalidOperationException("Memory must be disposed before returning to RMP.");

            if (memory.PoolIndex != PoolIdx)
                ThrowNotFromPool<RetainableMemory<T>>();

            // Determine with what bucket this buffer length is associated
            var bucketIndex = SelectBucketIndex(memory.LengthPow2);

            var pooled = false;

            // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
            if (bucketIndex < _buckets.Length)
            {
                var bucket = _buckets[bucketIndex];
                if (memory.LengthPow2 != bucket.BufferLength)
                    ThrowNotFromPool<RetainableMemory<T>>();

#pragma warning disable 618
                // Clear the memory if the user requests regardless of pooling result.
                // If not pooled then it should be RM.DisposeFinalize-d and destruction
                // is not always GC.
                if ((clearMemory || IsRentAlwaysClean || _typeHasReferences) && !memory.AlreadyClean)
                    memory.GetSpan().Clear();
                memory.AlreadyClean = false;
#pragma warning restore 618

                pooled = bucket.Return(memory);
            }

            // Log that the buffer was returned
            var log = RetainableMemoryPoolEventSource.Log;
            if (log.IsEnabled())
                log.BufferReturned(memory.GetHashCode(), memory.Length, Id);

            return pooled;
        }

        internal void PrintStats()
        {
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine($"{this.GetType().Namespace} stats:");
            foreach (var bucket in _buckets)
            {
                Console.WriteLine($"{bucket.BufferLength}: capacity {bucket.Capacity} index {bucket.Index} pooled {bucket.Pooled}");
            }

            Console.WriteLine("----------------------------------------------");
        }

        protected override void Dispose(bool disposing)
        {
            lock (this)
            {
                if (_disposed)
                    return;
                
                if (this == Default && !(Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload()))
                    ThrowHelper.ThrowInvalidOperationException("Disposing default retained memory pool is only possible during application shutdown");

                _disposed = true;
                foreach (var bucket in _buckets)
                {
                    bucket.Dispose();
                }
            }
        }

        [Obsolete("For diagnostic only")]
        internal IEnumerable<RetainableMemory<T>> InspectObjects()
        {
            foreach (var bucket in _buckets)
            {
                // ReSharper disable once HeapView.ObjectAllocation.Possible
                // ReSharper disable once HeapView.ObjectAllocation
                foreach (var buffer in bucket.EnumerateItems())
                {
                    if (buffer != null)
                    {
                        yield return buffer;
                    }
                }
            }
        }

        public override int MaxBufferSize => MaxBufferLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SelectBucketIndex(int bufferSize)
        {
            Debug.Assert(bufferSize >= 0);
            return Math.Max(0, (32 - BitUtils.LeadingZeroCount(bufferSize - 1)) - _minBufferLengthPow2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetMaxSizeForBucket(int binIndex)
        {
            int maxSize = MinBufferLength << binIndex;
            ThrowHelper.DebugAssert(maxSize >= 0);
            return maxSize;
        }

        private sealed class MemoryBucket : PerCoreObjectPool<RetainableMemory<T>, PerCoreMemoryBucket, PerCoreMemoryBucketWrapper>
        {
            private readonly RetainableMemoryPool<T> _pool;
            internal readonly int BufferLength;
            internal int Capacity => _perCorePools.Sum(p => p.Pool.Capacity);
            internal int Index => _perCorePools.Sum(p => p.Pool.Index);
            internal int Pooled => _perCorePools.Sum(p => p.Pool.EnumerateItems().Count(x => x != null));

            public MemoryBucket(RetainableMemoryPool<T> pool, int bufferLength, int perCoreSize)
                : base(() => new PerCoreMemoryBucket(() =>
                    {
                        var memory = pool.Factory(bufferLength);
                        // AtomicCounter.Dispose(ref memory.CounterRef);
                        // ThrowHelper.DebugAssert(memory.IsPooled);
                        return memory;
                    }, perCoreSize),
                    () => null, // RMP could look inside larger-size buckets and then allocates explicitly
                    unbounded: false)
            {
                _pool = pool;
                BufferLength = bufferLength;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public RetainableMemory<T> CreateNew()
            {
                return _pool.Factory(BufferLength);
            }
        }

        private struct PerCoreMemoryBucketWrapper : IObjectPoolWrapper<RetainableMemory<T>, PerCoreMemoryBucket>
        {
            public PerCoreMemoryBucket Pool
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get;
                set;
            }

            public void Dispose()
            {
                ((IDisposable) Pool).Dispose();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RetainableMemory<T>? Rent()
            {
                return Pool.Rent();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Return(RetainableMemory<T> obj)
            {
                return Pool.Return(obj);
            }
        }

        private sealed class PerCoreMemoryBucket : LockedObjectPoolCore<RetainableMemory<T>>
        {
#pragma warning disable 169
            private readonly Padding64 _padding64;
            private readonly Padding32 _padding32;
#pragma warning restore 169

            public PerCoreMemoryBucket(Func<RetainableMemory<T>> factory, int perCoreSize)
                : base(factory, perCoreSize, allocateOnEmpty: false)
            {
            }

            public override void Dispose()
            {
                foreach (var element in _items)
                {
                    var memory = element.Value;
                    // ReSharper disable once UseNullPropagation : Debug
                    if (memory != null)
                    {
                        ThrowHelper.Assert(memory.IsDisposed);
                        memory.PoolIndex = 1;
                        GC.SuppressFinalize(memory);
                        memory.Free(false);
                    }
                }
            }
        }
    }

    [EventSource(Guid = "C5BB9D49-21E4-4339-B6BC-981486D123DB", Name = "Spreads.Buffers." + nameof(RetainableMemoryPoolEventSource))]
    internal sealed class RetainableMemoryPoolEventSource : EventSource
    {
        internal static readonly RetainableMemoryPoolEventSource Log = new RetainableMemoryPoolEventSource();

        /// <summary>The reason for a BufferAllocated event.</summary>
        internal enum BufferAllocatedReason : int
        {
            /// <summary>The pool is allocating a buffer to be pooled in a bucket.</summary>
            Pooled,

            /// <summary>The requested buffer size was too large to be pooled.</summary>
            OverMaximumSize,

            /// <summary>The pool has already allocated for pooling as many buffers of a particular size as it's allowed.</summary>
            PoolExhausted
        }

        private RetainableMemoryPoolEventSource() : base(
            "Spreads.Buffers." + nameof(RetainableMemoryPoolEventSource))
        {
        }

        /// <summary>
        /// Event for when a buffer is rented.  This is invoked once for every successful call to Rent,
        /// regardless of whether a buffer is allocated or a buffer is taken from the pool.  In a
        /// perfect situation where all rented buffers are returned, we expect to see the number
        /// of BufferRented events exactly match the number of BuferReturned events, with the number
        /// of BufferAllocated events being less than or equal to those numbers (ideally significantly
        /// less than).
        /// </summary>
        [Event(1, Level = EventLevel.Verbose)]
        internal unsafe void BufferRented(int bufferId, int bufferSize, int poolId, int bucketId)
        {
            EventData* payload = stackalloc EventData[4];
            payload[0].Size = sizeof(int);
            payload[0].DataPointer = ((IntPtr) (&bufferId));
            payload[1].Size = sizeof(int);
            payload[1].DataPointer = ((IntPtr) (&bufferSize));
            payload[2].Size = sizeof(int);
            payload[2].DataPointer = ((IntPtr) (&poolId));
            payload[3].Size = sizeof(int);
            payload[3].DataPointer = ((IntPtr) (&bucketId));
            WriteEventCore(1, 4, payload);
        }

        /// <summary>
        /// Event for when a buffer is allocated by the pool.  In an ideal situation, the number
        /// of BufferAllocated events is significantly smaller than the number of BufferRented and
        /// BufferReturned events.
        /// </summary>
        [Event(2, Level = EventLevel.Informational)]
        internal unsafe void BufferAllocated(int bufferId, int bufferSize, int poolId, int bucketId, BufferAllocatedReason reason)
        {
            EventData* payload = stackalloc EventData[5];
            payload[0].Size = sizeof(int);
            payload[0].DataPointer = ((IntPtr) (&bufferId));
            payload[1].Size = sizeof(int);
            payload[1].DataPointer = ((IntPtr) (&bufferSize));
            payload[2].Size = sizeof(int);
            payload[2].DataPointer = ((IntPtr) (&poolId));
            payload[3].Size = sizeof(int);
            payload[3].DataPointer = ((IntPtr) (&bucketId));
            payload[4].Size = sizeof(BufferAllocatedReason);
            payload[4].DataPointer = ((IntPtr) (&reason));
            WriteEventCore(2, 5, payload);
        }

        /// <summary>
        /// Event raised when a buffer is returned to the pool.  This event is raised regardless of whether
        /// the returned buffer is stored or dropped.  In an ideal situation, the number of BufferReturned
        /// events exactly matches the number of BufferRented events.
        /// </summary>
        [Event(3, Level = EventLevel.Verbose)]
        internal void BufferReturned(int bufferId, int bufferSize, int poolId) => WriteEvent(3, bufferId, bufferSize, poolId);

        /// <summary>
        /// Event raised when we attempt to free a buffer due to inactivity or memory pressure (by no longer
        /// referencing it). It is possible (although not commmon) this buffer could be rented as we attempt
        /// to free it. A rent event before or after this event for the same ID, is a rare, but expected case.
        /// </summary>
        [Event(4, Level = EventLevel.Informational)]
        internal void BufferTrimmed(int bufferId, int bufferSize, int poolId) => WriteEvent(4, bufferId, bufferSize, poolId);

        /// <summary>
        /// Event raised when we check to trim buffers.
        /// </summary>
        [Event(5, Level = EventLevel.Informational)]
        internal void BufferTrimPoll(int milliseconds, int pressure) => WriteEvent(5, milliseconds, pressure);
    }
}