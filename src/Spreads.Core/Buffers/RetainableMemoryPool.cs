// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Origianlly from coreclr licenced as MIT. See LICENSE.Dependencies.txt file in the repo root for the full license.
// https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/ConfigurableArrayPool.cs
// Supports custom minimum size instead of 16 and maxBucketsToTry is configurable and could be zero.
// Return returns bool=true when an object is pooled.

using Spreads.Serialization;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    [Obsolete("26MOPS vs 44MOPS for premature abstraction")]
    internal abstract class RetainableMemoryPoolBase<T, TImpl> : MemoryPool<T> where TImpl : RetainableMemory<T>
    {
        public abstract TImpl RentMemory(int minimumLength);

        public abstract bool Return(TImpl memory, bool clearArray = false);

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            return RentMemory(minBufferSize);
        }
    }

    /// <summary>
    /// This is thread-safe only with correct usage. With Rent a buffer must be returned via Return and used only by a single
    /// thread at a time and no refcounting is done. As soon as a buffer is Retain-ed it must only be used with ref-counting
    /// methods such as Increment/Decrement and Dispose of RetainedMemory. A buffer is returned to a pool when refcount
    /// reaches zero.
    /// </summary>
    public class RetainableMemoryPool<T> : MemoryPool<T>
    {
        internal static RetainableMemoryPool<T>[] KnownPools = new RetainableMemoryPool<T>[256];
        internal byte PoolIdx;

        /// <summary>
        /// Set to true to always clean on return and clean buffers produced by factory.
        /// </summary>
        public readonly bool IsRentAlwaysClean;

        private readonly bool _typeHasReferences = !TypeHelper<T>.IsPinnable;

        private readonly Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> _factory;
        private readonly int _minBufferLength;
        private readonly int _maxBufferLength;
        private readonly int _maxBucketsToTry;
        private readonly bool _pin;
        private const int DefaultMinArrayLength = 2048;

        /// <summary>The default maximum length of each array in the pool (2^20).</summary>
        private const int DefaultMaxArrayLength = 1024 * 1024;

        /// <summary>The default maximum number of arrays per bucket that are available for rent.</summary>
        private const int DefaultMaxNumberOfArraysPerBucket = Settings.SlabLength / DefaultMinArrayLength; // 128kb / 2kb = 64

        private readonly Bucket[] _buckets;
        private readonly int _minBufferLengthPow2;
        internal bool _disposed;
        internal bool AddStackTraceOnRent = false;

        public RetainableMemoryPool(Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> factory)
            : this(factory, DefaultMinArrayLength, DefaultMaxArrayLength, DefaultMaxNumberOfArraysPerBucket)
        { }

        public RetainableMemoryPool(Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> factory, int minLength,
            int maxLength, int maxBuffersPerBucket, int maxBucketsToTry = 2, bool pin = false, bool rentAlwaysClean = false)
        {
            IsRentAlwaysClean = rentAlwaysClean;
            _factory = factory;

            if (minLength <= 16)
            {
                minLength = 16;
            }

            _minBufferLengthPow2 = 32 - IntUtil.NumberOfLeadingZeros(minLength - 1);
            _minBufferLength = 1 << _minBufferLengthPow2;

            if (maxBucketsToTry < 0)
            {
                maxBucketsToTry = 0;
            }
            if (maxBucketsToTry > 4)
            {
                maxBucketsToTry = 4;
            }

            _maxBucketsToTry = maxBucketsToTry;

            if (pin && !TypeHelper<T>.IsPinnable)
            {
                ThrowHelper.ThrowInvalidOperationException($"Type {typeof(T).Name} is not pinnable. Cannot create RetainableMemoryPool with pinnable option.");
            }

            _pin = pin;

            if (maxLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            }
            if (maxBuffersPerBucket <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBuffersPerBucket));
            }

            // Our bucketing algorithm has a min length of 2^4 and a max length of 2^30.
            // Constrain the actual max used to those values.
            const int maximumArrayLength = 0x40000000;

            if (maxLength > maximumArrayLength)
            {
                maxLength = maximumArrayLength;
            }
            else if (maxLength < minLength)
            {
                maxLength = minLength;
            }

            _maxBufferLength = maxLength;

            // Create the buckets.
            int poolId = Id;
            int maxBuckets = SelectBucketIndex(maxLength);
            var buckets = new Bucket[maxBuckets + 1];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new Bucket(this, _factory, GetMaxSizeForBucket(i), maxBuffersPerBucket, poolId);
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
                        PoolIdx = checked((byte)i);
                        KnownPools[i] = this;
                        return;
                    }
                }
                ThrowHelper.ThrowInvalidOperationException("KnownPools slots exhausted. 254 pools ought to be enough for anybody.");
            }
        }

        /// <summary>Gets an ID for the pool to use with events.</summary>
        private int Id => GetHashCode();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private RetainableMemory<T> CreateNew(int length)
        {
            if (_disposed)
            {
                ThrowDisposed<RetainableMemoryPool<T>>();
            }

            if (_factory == null)
            {
                var am = ArrayMemory<T>.Create(BufferPool<T>.Rent(length), 0, length, false, _pin, this);
                // am._pool = this;
                return Unsafe.As<RetainableMemory<T>>(am);
            }

            var buffer = _factory.Invoke(this, length);
            if (IsRentAlwaysClean)
            {
                buffer.GetSpan().Clear();
            }
            return buffer;
        }

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            if (minBufferSize == -1)
            {
                minBufferSize = _minBufferLength;
            }
            return RentMemory(minBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainableMemory<T> RentMemory(int minimumLength)
        {
            // Arrays can't be smaller than zero.  We allow requesting zero-length arrays (even though
            // pooling such an array isn't valuable) as it's a valid length array, and we want the pool
            // to be usable in general instead of using `new`, even for computed lengths.
            if (minimumLength <= 0)
            {
                ThrowBadLength();
            }

            var log = RetainableMemoryPoolEventSource.Log;
            RetainableMemory<T> buffer;

            int index = SelectBucketIndex(minimumLength);
            if (index < _buckets.Length)
            {
                // Search for an array starting at the 'index' bucket. If the bucket is empty, bump up to the
                // next higher bucket and try that one, but only try at most a few buckets.
                int i = index;
                do
                {
                    // Attempt to rent from the bucket.  If we get a buffer from it, return it.
                    buffer = _buckets[i].Rent();
                    if (buffer != null)
                    {
                        if (log.IsEnabled())
                        {
                            log.BufferRented(buffer.GetHashCode(), buffer.Length, Id, _buckets[i].Id);
                        }
                        buffer._isPooled = false;
                        if (AddStackTraceOnRent)
                        {
                            buffer.Tag = Environment.StackTrace;
                        }
                        return buffer;
                    }
                }
                while (++i < _buckets.Length && i <= index + _maxBucketsToTry);

                // The pool was exhausted for this buffer size.  Allocate a new buffer with a size corresponding
                // to the appropriate bucket.
                buffer = _buckets[index].CreateNew();
            }
            else
            {
                // The request was for a size too large for the pool.  Allocate an array of exactly the requested length.
                // When it's returned to the pool, we'll simply throw it away.
                buffer = CreateNew(minimumLength);
            }

            if (log.IsEnabled())
            {
                int bufferId = buffer.GetHashCode(), bucketId = -1; // no bucket for an on-demand allocated buffer
                log.BufferRented(bufferId, buffer.Length, Id, bucketId);
                log.BufferAllocated(bufferId, buffer.Length, Id, bucketId, index >= _buckets.Length ?
                    RetainableMemoryPoolEventSource.BufferAllocatedReason.OverMaximumSize : RetainableMemoryPoolEventSource.BufferAllocatedReason.PoolExhausted);
            }

            if (AddStackTraceOnRent)
            {
                buffer.Tag = Environment.StackTrace;
            }
            return buffer;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public bool Return(RetainableMemory<T> memory, bool clearMemory = true)
        {
            return ReturnInternal(memory, clearMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ReturnInternal(RetainableMemory<T> memory, bool clearMemory = true)
        {
            if (_disposed)
            {
                return false;
            }

            if (memory._poolIdx != PoolIdx)
            {
                if (memory.IsDisposed)
                {
                    ThrowDisposed<RetainableMemory<T>>();
                }
                else
                {
                    ThrowNotFromPool<RetainableMemory<T>>();
                }
            }

            if (memory._isPooled)
            {
                ThrowAlreadyPooled<RetainableMemory<T>>();
            }

            // Determine with what bucket this array length is associated
            int bucket = SelectBucketIndex(memory.LengthPow2);

            // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
            if (bucket < _buckets.Length)
            {
                // Clear the array if the user requests regardless of pooling result.
                // If not pooled then it should be RM.DisposeFinalize-d and destruction
                // is not always GC.
                if (clearMemory || IsRentAlwaysClean || _typeHasReferences)
                {
                    if (!memory.SkipCleaning)
                    {
                        memory.GetSpan().Clear();
                    }
                }

                memory.SkipCleaning = false;

                // Return the buffer to its bucket.  In the future, we might consider having Return return false
                // instead of dropping a bucket, in which case we could try to return to a lower-sized bucket,
                // just as how in Rent we allow renting from a higher-sized bucket.
                _buckets[bucket].Return(memory);
            }

            // Log that the buffer was returned
            var log = RetainableMemoryPoolEventSource.Log;
            if (log.IsEnabled())
            {
                log.BufferReturned(memory.GetHashCode(), memory.Length, Id);
            }

            return memory._isPooled;
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            foreach (var bucket in _buckets)
            {
                foreach (var retainableMemory in bucket._buffers)
                {
                    var disposable = retainableMemory;
                    // ReSharper disable once UseNullPropagation : Debug
                    if (disposable != null)
                    {
                        // keep all flags but clear the counter
                        retainableMemory.CounterRef &= ~AtomicCounter.CountMask;

                        retainableMemory._isPooled = false;
                        // must keep retainableMemory._poolIdx
                        retainableMemory.DisposeFinalize();
                    }
                }
            }
        }

        [Obsolete("For diagnostic only")]
        internal IEnumerable<RetainableMemory<T>> InspectObjects()
        {
            foreach (var bucket in _buckets)
            {
                foreach (var buffer in bucket._buffers)
                {
                    if (buffer != null)
                    {
                        yield return buffer;
                    }
                }
            }
        }

        public override int MaxBufferSize => _maxBufferLength;

        /// <summary>Provides a thread-safe bucket containing buffers that can be Rent'd and Return'd.</summary>
        private sealed class Bucket
        {
            private readonly RetainableMemoryPool<T> _pool;
            private readonly Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> _factory;
            private readonly int _bufferLength;
            internal readonly RetainableMemory<T>[] _buffers;
            private readonly int _poolId;

            private int _index;
            private int _locker;

            private ArrayMemorySliceBucket<T> _sliceBucket;

            /// <summary>
            /// Creates the pool with numberOfBuffers buffers where each buffer is of bufferLength length.
            /// </summary>
            internal Bucket(RetainableMemoryPool<T> pool, Func<RetainableMemoryPool<T>, int, RetainableMemory<T>> factory,
                int bufferLength, int numberOfBuffers, int poolId)
            {
                // _lock = new SpinLock(Debugger.IsAttached); // only enable thread tracking if debugger is attached; it adds non-trivial overheads to Enter/Exit
                _buffers = new RetainableMemory<T>[numberOfBuffers];

                _pool = pool;

                _factory = factory;

                _bufferLength = bufferLength;
                _poolId = poolId;
            }

            /// <summary>Gets an ID for the bucket to use with events.</summary>
            internal int Id => GetHashCode();

            [MethodImpl(MethodImplOptions.NoInlining)]
            internal RetainableMemory<T> CreateNew()
            {
                RetainableMemory<T> buffer;

                if (_pool._disposed)
                {
                    ThrowDisposed<RetainableMemoryPool<T>>();
                }

                if (_factory == null)
                {
                    ArrayMemory<T> arrayMemory;
                    if (_pool._pin && _bufferLength * Unsafe.SizeOf<T>() <= Settings.LARGE_BUFFER_LIMIT)
                    {
                        if (_sliceBucket == null)
                        {
                            _sliceBucket = new ArrayMemorySliceBucket<T>(_bufferLength, _buffers.Length);
                        }

                        arrayMemory = _sliceBucket.RentMemory();
                    }
                    else
                    {
                        arrayMemory = ArrayMemory<T>.Create(_bufferLength, _pool._pin);
                    }

                    arrayMemory._poolIdx = _pool.PoolIdx;
                    if (arrayMemory.LengthPow2 != _bufferLength)
                    {
                        // TODO proper exception, this is for args
                        ThrowBadLength();
                    }
                    buffer = arrayMemory;
                }
                else
                {
                    buffer = _factory.Invoke(_pool, _bufferLength);
                }

                if (_pool.IsRentAlwaysClean)
                {
                    buffer.GetSpan().Clear();
                }
                return buffer;
            }

            /// <summary>Takes an array from the bucket.  If the bucket is empty, returns null.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal RetainableMemory<T> Rent()
            {
                RetainableMemory<T>[] buffers = _buffers;
                RetainableMemory<T> buffer = null;

                // While holding the lock, grab whatever is at the next available index and
                // update the index.  We do as little work as possible while holding the spin
                // lock to minimize contention with other threads.  The try/finally is
                // necessary to properly handle thread aborts on platforms which have them.
                // bool lockTaken = false;
                var allocateBuffer = false;
#if !NETCOREAPP
                try
#endif
                {
                    var spinner = new SpinWait();
                    while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                    {
                        spinner.SpinOnce();
                    }

                    if (_index < buffers.Length)
                    {
                        buffer = buffers[_index];
                        buffers[_index++] = null;
                        allocateBuffer = buffer == null;
                    }
                }
#if !NETCOREAPP
                finally
#endif
                {
                    Volatile.Write(ref _locker, 0);
                }

                // While we were holding the lock, we grabbed whatever was at the next available index, if
                // there was one.  If we tried and if we got back null, that means we hadn't yet allocated
                // for that slot, in which case we should do so now.
                if (allocateBuffer)
                {
                    buffer = CreateNew();

                    var log = RetainableMemoryPoolEventSource.Log;
                    if (log.IsEnabled())
                    {
                        log.BufferAllocated(buffer.GetHashCode(), _bufferLength, _poolId, Id,
                            RetainableMemoryPoolEventSource.BufferAllocatedReason.Pooled);
                    }
                }
                else
                {
                    if (buffer != null)
                    {
                        if (!buffer._isPooled)
                        {
                            ThrowNotFromPool<RetainableMemory<T>>();
                        }

                        Debug.Assert(buffer.IsDisposed);

                        // Set counter to zero, keep other flags
                        // Do not need atomic CAS here because the buffer is inside the
                        // pool and this assignment is inside the spin lock
                        buffer.CounterRef &= ~AtomicCounter.CountMask;
                    }
                }

                return buffer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Return(RetainableMemory<T> memory)
            {
                // Check to see if the buffer is the correct size for this bucket
                if (memory.LengthPow2 != _bufferLength)
                {
                    ThrowNotFromPool<RetainableMemory<T>>();
                }

                // While holding the spin lock, if there's room available in the bucket,
                // put the buffer into the next available slot.  Otherwise, we just drop it.
                // The try/finally is necessary to properly handle thread aborts on platforms
                // which have them.
                int disposed = 0;
#if !NETCOREAPP
                try
#endif
                {
                    var spinner = new SpinWait();
                    while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                    {
                        spinner.SpinOnce();
                    }

                    var pooled = _index != 0;

                    if (pooled)
                    {
                        if ((disposed = AtomicCounter.TryDispose(ref memory.CounterRef)) == 0)
                        {
                            _buffers[--_index] = memory;
                            Debug.Assert(AtomicCounter.GetIsDisposed(ref memory.CounterRef));
                            memory._isPooled = true;
                        }
                    }
                    else
                    {
                        memory.DisposeFinalize();
                    }
                }
#if !NETCOREAPP
                finally
#endif
                {
                    Volatile.Write(ref _locker, 0);
                }
                // after unlock
                if (disposed != 0)
                {
                    AtomicCounter.ThrowNonZeroTryDispose(disposed);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SelectBucketIndex(int bufferSize)
        {
            Debug.Assert(bufferSize >= 0);

            var intUtil = Math.Max(0, (32 - IntUtil.NumberOfLeadingZeros(bufferSize - 1)) - _minBufferLengthPow2);

#if DEBUG
            // TODO remove this check after some usage, see if this is not the same on some edge case

            // bufferSize of 0 will underflow here, causing a huge
            // index which the caller will discard because it is not
            // within the bounds of the bucket array.
            uint bitsRemaining = ((uint)bufferSize - 1) >> _minBufferLengthPow2;

            int poolIndex = 0;
            if (bitsRemaining > 0xFFFF) { bitsRemaining >>= 16; poolIndex = 16; }
            if (bitsRemaining > 0xFF) { bitsRemaining >>= 8; poolIndex += 8; }
            if (bitsRemaining > 0xF) { bitsRemaining >>= 4; poolIndex += 4; }
            if (bitsRemaining > 0x3) { bitsRemaining >>= 2; poolIndex += 2; }
            if (bitsRemaining > 0x1) { bitsRemaining >>= 1; poolIndex += 1; }
            var manual = poolIndex + (int)bitsRemaining;

            Debug.Assert(manual == intUtil);
#endif
            return intUtil;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetMaxSizeForBucket(int binIndex)
        {
            int maxSize = _minBufferLength << binIndex;
            Debug.Assert(maxSize >= 0);
            return maxSize;
        }
    }

    [EventSource(Guid = "C5BB9D49-21E4-4339-B6BC-981486D123DB", Name = "Spreads.Buffers.MemoryManagerPoolEventSource")]
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
            "Spreads.Buffers.MemoryManagerPoolEventSource")
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
            payload[0].DataPointer = ((IntPtr)(&bufferId));
            payload[1].Size = sizeof(int);
            payload[1].DataPointer = ((IntPtr)(&bufferSize));
            payload[2].Size = sizeof(int);
            payload[2].DataPointer = ((IntPtr)(&poolId));
            payload[3].Size = sizeof(int);
            payload[3].DataPointer = ((IntPtr)(&bucketId));
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
            payload[0].DataPointer = ((IntPtr)(&bufferId));
            payload[1].Size = sizeof(int);
            payload[1].DataPointer = ((IntPtr)(&bufferSize));
            payload[2].Size = sizeof(int);
            payload[2].DataPointer = ((IntPtr)(&poolId));
            payload[3].Size = sizeof(int);
            payload[3].DataPointer = ((IntPtr)(&bucketId));
            payload[4].Size = sizeof(BufferAllocatedReason);
            payload[4].DataPointer = ((IntPtr)(&reason));
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
