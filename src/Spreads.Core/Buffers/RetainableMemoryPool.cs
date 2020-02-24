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
        internal static readonly RetainableMemoryPool<T>[] KnownPools = new RetainableMemoryPool<T>[64];
        public readonly byte PoolIdx;

        /// <summary>
        /// Set to true to always clean on return and clean buffers produced by the factory provided to the constructor.
        /// </summary>
        public readonly bool IsRentAlwaysClean;

        public readonly int MinBufferLength;
        public readonly int MaxBufferLength;
        public readonly int MaxBucketsToTry;

        private readonly bool _typeHasReferences = TypeHelper<T>.IsReferenceOrContainsReferences;

        internal readonly Func<int, int, RetainableMemory<T>> Factory;

        /// <summary>The default minimum length of each memory buffer in the pool.</summary>
        public const int DefaultMinBufferLength = 2048;

        /// <summary>The default maximum length of each memory buffer in the pool (2^20).</summary>
        public const int DefaultMaxBufferLength = 1024 * 1024;

        /// <summary>The default maximum number of memory buffers per bucket per core that are available for rent.</summary>
        public const int DefaultMaxNumberOfBuffersPerBucketPerCore = 8;

        private readonly MemoryBucket[] _buckets;

        private readonly int _minBufferLengthPow2;
        internal volatile bool _disposed;

        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        internal bool AddStackTraceOnRent = LeaksDetection.Enabled;

        public RetainableMemoryPool(Func<RetainableMemoryPool<T>, int, RetainableMemory<T>>? factory = null,
            int minLength = DefaultMinBufferLength,
            int maxLength = DefaultMaxBufferLength,
            int maxBuffersPerBucketPerCore = DefaultMaxNumberOfBuffersPerBucketPerCore,
            int maxBucketsToTry = 2,
            bool rentAlwaysClean = false)
        {
            IsRentAlwaysClean = rentAlwaysClean;

            Factory = (length, cpuId) =>
            {
                var memory = factory == null ? PrivateMemory<T>.Create(length, this, cpuId) : factory(this, length);
                if (IsRentAlwaysClean)
                    memory.GetSpan().Clear();
                return memory;
            };

            if (minLength <= 16)
            {
                minLength = 16;
            }

            _minBufferLengthPow2 = 32 - BitUtil.NumberOfLeadingZeros(minLength - 1);
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

            if (maxBuffersPerBucketPerCore <= 0)
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

            lock (KnownPools)
            {
                // start from 2,
                // pool idx == 0 is always null which means a buffer is not from pool
                // pool idx == 1 means a buffer is from default pool, e.g. static array pool
                for (int i = 2; i < KnownPools.Length; i++)
                {
                    if (KnownPools[i] == null)
                    {
                        PoolIdx = checked((byte) i);
                        KnownPools[i] = this;
                        return;
                    }
                }

                ThrowHelper.ThrowInvalidOperationException("KnownPools slots exhausted. 64 pools ought to be enough for anybody.");
            }
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
            return RentMemory(minBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainableMemory<T> RentMemory(int minBufferSize = -1)
        {
            var maxBucketsToTry = MaxBucketsToTry; // TODO per call

            var cpuId = Cpu.GetCurrentCoreId();

            minBufferSize = Math.Max(MinBufferLength, minBufferSize);

            var log = RetainableMemoryPoolEventSource.Log;
            RetainableMemory<T> buffer;

            int bucketIndex = SelectBucketIndex(minBufferSize);

            if (bucketIndex < _buckets.Length)
            {
                int i = bucketIndex;

                // Search for a buffer starting at the 'index' bucket. If the bucket is empty, bump up to the
                // next higher bucket and try that one, but only try at most a few buckets.
                do
                {
                    buffer = _buckets[i].Rent(cpuId);
                } while (buffer == null
                         && ++i < _buckets.Length
                         && i <= bucketIndex + maxBucketsToTry);

                if (buffer != null)
                {
                    ThrowHelper.DebugAssert(!buffer.IsDisposed && buffer.ReferenceCount == 0, "!buffer.IsDisposed");

                    if (log.IsEnabled())
                        log.BufferRented(buffer.GetHashCode(), buffer.Length, Id, _buckets[i].GetHashCode());

                    buffer.IsPooled = false;

                    if (AddStackTraceOnRent)
                        buffer.Tag = Environment.StackTrace;

                    return buffer;
                }

                // The pool was exhausted for this buffer size. Allocate a new buffer
                // with a size corresponding to the appropriate bucket.
                buffer = _buckets[bucketIndex].CreateNew(cpuId);
                ThrowHelper.DebugAssert(!buffer.IsDisposed, "_buckets[index].CreateNew(); returned disposed buffer");
            }
            else
            {
                // The request was for a size too large for the pool. Allocate a buffer of exactly the requested length.
                // When it's returned to the pool, we'll simply throw it away.
                buffer = Factory(minBufferSize, cpuId);
                ThrowHelper.DebugAssert(!buffer.IsDisposed, "Factory returned disposed buffer");
            }

            if (log.IsEnabled())
            {
                int bufferId = buffer.GetHashCode(), bucketId = -1; // no bucket for an on-demand allocated buffer
                log.BufferRented(bufferId, buffer.Length, Id, bucketId);
                log.BufferAllocated(bufferId, buffer.Length, Id, bucketId,
                    bucketIndex >= _buckets.Length
                        ? RetainableMemoryPoolEventSource.BufferAllocatedReason.OverMaximumSize
                        : RetainableMemoryPoolEventSource.BufferAllocatedReason.PoolExhausted);
            }
#if DEBUG
            if (AddStackTraceOnRent)
            {
                buffer.Tag = Environment.StackTrace;
            }
#endif
            return buffer;
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

            if (memory.PoolIndex != PoolIdx)
            {
                if (memory.IsDisposed)
                    ThrowDisposed<RetainableMemory<T>>();
                else
                    ThrowNotFromPool<RetainableMemory<T>>();
            }

            if (memory.IsPooled)
                ThrowAlreadyPooled<RetainableMemory<T>>();

            // Determine with what bucket this buffer length is associated
            int bucket = SelectBucketIndex(memory.LengthPow2);

            // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
            if (bucket < _buckets.Length)
            {
                // Clear the memory if the user requests regardless of pooling result.
                // If not pooled then it should be RM.DisposeFinalize-d and destruction
                // is not always GC.
                if (clearMemory || IsRentAlwaysClean || _typeHasReferences)
                {
                    if (!memory.SkipCleaning)
                        memory.GetSpan().Clear();
                }
                
                memory.SkipCleaning = false;

                {
                    // Here we own memory and try to return it. If return is unsuccessful
                    // then we still do own the instance and could unset the property 
                    // to false. But if we set the property to the return value of
                    // bucket.Return then in the true case the memory could be already 
                    // rented by the time the field is set, so don't do that: `memory.IsPooled = _buckets[bucket].Return(memory)`
                    memory.IsPooled = true;
                    var reallyPooled = _buckets[bucket].Return(memory);
                    if (!reallyPooled)
                        memory.IsPooled = false;
                }
            }

            // Log that the buffer was returned
            var log = RetainableMemoryPoolEventSource.Log;
            if (log.IsEnabled())
                log.BufferReturned(memory.GetHashCode(), memory.Length, Id);

            return memory.IsPooled;
        }

        internal void PrintStats()
        {
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine($"{this.GetType().Namespace} stats:");
            foreach (var bucket in _buckets)
            {
                Console.WriteLine($"{bucket.BufferLength}"); // TODO aggregate per-core values
                throw new NotImplementedException();
                // Console.WriteLine($"{bucket.BufferLength}: capacity {bucket._buffers.Length} index {bucket._index} pooled {bucket._buffers.Count(x => x != null)}");
            }

            Console.WriteLine("----------------------------------------------");
        }

        protected override void Dispose(bool disposing)
        {
            lock (this)
            {
                if (_disposed)
                    return;
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

            var intUtil = Math.Max(0, (32 - BitUtil.NumberOfLeadingZeros(bufferSize - 1)) - _minBufferLengthPow2);

#if DEBUG
            // TODO remove this check after some usage, see if this is not the same on some edge case

            // bufferSize of 0 will underflow here, causing a huge
            // index which the caller will discard because it is not
            // within the bounds of the bucket array.
            uint bitsRemaining = ((uint) bufferSize - 1) >> _minBufferLengthPow2;

            int poolIndex = 0;
            if (bitsRemaining > 0xFFFF)
            {
                bitsRemaining >>= 16;
                poolIndex = 16;
            }

            if (bitsRemaining > 0xFF)
            {
                bitsRemaining >>= 8;
                poolIndex += 8;
            }

            if (bitsRemaining > 0xF)
            {
                bitsRemaining >>= 4;
                poolIndex += 4;
            }

            if (bitsRemaining > 0x3)
            {
                bitsRemaining >>= 2;
                poolIndex += 2;
            }

            if (bitsRemaining > 0x1)
            {
                bitsRemaining >>= 1;
                poolIndex += 1;
            }

            var manual = poolIndex + (int) bitsRemaining;

            ThrowHelper.DebugAssert(manual == intUtil);
#endif
            return intUtil;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetMaxSizeForBucket(int binIndex)
        {
            int maxSize = MinBufferLength << binIndex;
            ThrowHelper.DebugAssert(maxSize >= 0);
            return maxSize;
        }

        private sealed class MemoryBucket : PerCoreObjectPool<RetainableMemory<T>, PerCoreMemoryBucket>
        {
            private readonly RetainableMemoryPool<T> _pool;
            internal readonly int BufferLength;

            public MemoryBucket(RetainableMemoryPool<T> pool, int bufferLength, int perCoreSize)
                : base(() => new PerCoreMemoryBucket(() => pool.Factory(bufferLength, Cpu.GetCurrentCoreId()), perCoreSize),
                    () => null, // RMP could look inside larger-size buckets and then allocates explicitly
                    unbounded: false)
            {
                _pool = pool;
                BufferLength = bufferLength;
            }

            public RetainableMemory<T> CreateNew(int cpuId)
            {
                return _pool.Factory(BufferLength, cpuId);
            }
        }

        private sealed class PerCoreMemoryBucket : LockedObjectPoolCore<RetainableMemory<T>>
        {
#pragma warning disable 169
            private readonly Padding64 _padding64;
            private readonly Padding64 _padding32;
#pragma warning restore 169

            public PerCoreMemoryBucket(Func<RetainableMemory<T>> factory, int perCoreSize) : base(factory, perCoreSize, allocateOnEmpty: false)
            {
            }

            public override void Dispose()
            {
                foreach (var retainableMemory in _items)
                {
                    var disposable = retainableMemory.Value;
                    // ReSharper disable once UseNullPropagation : Debug
                    if (disposable != null)
                    {
                        // keep all flags but clear the counter
                        disposable.CounterRef &= ~AtomicCounter.CountMask;
                        disposable.IsPooled = false;
                        // must keep retainableMemory._poolIdx
                        disposable.DisposeFinalize();
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