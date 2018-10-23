// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Origianlly from coreclr licenced as MIT. See LICENSE.Dependencies.txt file in the repo root for the full license.
// https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/ConfigurableArrayPool.cs
// Supports custom minimum size instead of 16 and maxBucketsToTry is configurable and could be zero.
// Return returns bool=true when an object is pooled.

using Spreads.Utils;
using System;
using System.Buffers;
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

    public sealed class RetainableMemoryPool<T, TImpl> : MemoryPool<T> where TImpl : RetainableMemory<T> // TODO
    {
        private readonly Func<int, TImpl> _factory;
        private readonly int _minBufferLength;
        private readonly int _maxBufferLength;
        private readonly int _maxBucketsToTry;
        private const int DefaultMinArrayLength = 2048;

        /// <summary>The default maximum length of each array in the pool (2^20).</summary>
        private const int DefaultMaxArrayLength = 1024 * 1024;

        /// <summary>The default maximum number of arrays per bucket that are available for rent.</summary>
        private const int DefaultMaxNumberOfArraysPerBucket = Settings.SlabLength / DefaultMinArrayLength; // 128kb / 2kb = 64

        private readonly Bucket[] _buckets;
        private readonly int _minBufferLengthPow2;

        internal RetainableMemoryPool(Func<int, TImpl> factory) : this(factory, DefaultMinArrayLength, DefaultMaxArrayLength, DefaultMaxNumberOfArraysPerBucket)
        {
        }

        internal RetainableMemoryPool(Func<int, TImpl> factory, int minBufferLength, int maxArrayLength, int maxArraysPerBucket, int maxBucketsToTry = 2)
        {
            _factory = factory;
            if (typeof(TImpl) != typeof(ArrayMemory<T>) && _factory == null)
            {
                ThrowArgumentNull<Func<int, TImpl>>();
            }

            if (minBufferLength <= 16)
            {
                minBufferLength = 16;
            }
            _minBufferLength = BitUtil.FindNextPositivePowerOfTwo(minBufferLength);
            _minBufferLengthPow2 = (int)Math.Log(_minBufferLength, 2);

            if (maxBucketsToTry < 0)
            {
                maxBucketsToTry = 0;
            }
            if (maxBucketsToTry > 4)
            {
                maxBucketsToTry = 4;
            }

            _maxBucketsToTry = maxBucketsToTry;

            if (maxArrayLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxArrayLength));
            }
            if (maxArraysPerBucket <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxArraysPerBucket));
            }

            // Our bucketing algorithm has a min length of 2^4 and a max length of 2^30.
            // Constrain the actual max used to those values.
            const int maximumArrayLength = 0x40000000;

            if (maxArrayLength > maximumArrayLength)
            {
                maxArrayLength = maximumArrayLength;
            }
            else if (maxArrayLength < minBufferLength)
            {
                maxArrayLength = minBufferLength;
            }

            _maxBufferLength = maxArrayLength;

            // Create the buckets.
            int poolId = Id;
            int maxBuckets = SelectBucketIndex(maxArrayLength);
            var buckets = new Bucket[maxBuckets + 1];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new Bucket(_factory, GetMaxSizeForBucket(i), maxArraysPerBucket, poolId);
            }
            _buckets = buckets;
        }

        /// <summary>Gets an ID for the pool to use with events.</summary>
        private int Id => GetHashCode();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TImpl CreateNew(int length)
        {
            if (typeof(TImpl) == typeof(ArrayMemory<T>) && _factory == null)
            {
                return Unsafe.As<TImpl>(ArrayMemory<T>.Create(length));
            }

            return _factory.Invoke(length);
        }

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            return RentMemory(minBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TImpl RentMemory(int minimumLength)
        {
            // Arrays can't be smaller than zero.  We allow requesting zero-length arrays (even though
            // pooling such an array isn't valuable) as it's a valid length array, and we want the pool
            // to be usable in general instead of using `new`, even for computed lengths.
            if (minimumLength <= 0)
            {
                ThrowBadLength();
            }

            var log = RetainedMemoryPoolEventSource.Log;
            TImpl buffer;

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
                    RetainedMemoryPoolEventSource.BufferAllocatedReason.OverMaximumSize : RetainedMemoryPoolEventSource.BufferAllocatedReason.PoolExhausted);
            }

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(TImpl memory, bool clearArray = false)
        {
            if (memory == null)
            {
                ThrowArgumentNull<TImpl>();
            }

            // ReSharper disable once PossibleNullReferenceException
            if (memory.IsDisposed)
            {
                ThrowDisposed<TImpl>();
            }

            // Determine with what bucket this array length is associated
            int bucket = SelectBucketIndex(memory.Length);

            var pooled = false;

            // Clear the array if the user requests
            if (clearArray)
            {
                memory.GetSpan().Clear();
            }

            // If we can tell that the buffer was allocated, drop it. Otherwise, check if we have space in the pool
            if (bucket < _buckets.Length)
            {
                // Return the buffer to its bucket.  In the future, we might consider having Return return false
                // instead of dropping a bucket, in which case we could try to return to a lower-sized bucket,
                // just as how in Rent we allow renting from a higher-sized bucket.
                pooled = _buckets[bucket].Return(memory);
            }
            else
            {
                // if !pooled then bucket.Return disposes so in else branch
                ((IDisposable)memory).Dispose();
            }

            // Log that the buffer was returned
            var log = RetainedMemoryPoolEventSource.Log;
            if (log.IsEnabled())
            {
                log.BufferReturned(memory.GetHashCode(), memory.Length, Id);
            }

            return pooled;
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var bucket in _buckets)
            {
                foreach (var sharedMemoryBuffer in bucket._buffers)
                {
                    var disposable = sharedMemoryBuffer;
                    // ReSharper disable once UseNullPropagation : Debug
                    if (disposable != null)
                    {
                        ((IDisposable)disposable).Dispose();
                    }
                }
            }
        }

        public override int MaxBufferSize => _maxBufferLength;

        /// <summary>Provides a thread-safe bucket containing buffers that can be Rent'd and Return'd.</summary>
        private sealed class Bucket
        {
            private readonly Func<int, TImpl> _factory;
            internal readonly int _bufferLength;
            internal readonly TImpl[] _buffers;
            private readonly int _poolId;

            private SpinLock _lock; // do not make this readonly; it's a mutable struct
            private int _index;

            private ArrayMemorySliceBucket<T> _sliceBucket;

            /// <summary>
            /// Creates the pool with numberOfBuffers arrays where each buffer is of bufferLength length.
            /// </summary>
            internal Bucket(Func<int, TImpl> factory, int bufferLength, int numberOfBuffers, int poolId)
            {
                _lock = new SpinLock(Debugger.IsAttached); // only enable thread tracking if debugger is attached; it adds non-trivial overheads to Enter/Exit
                _buffers = new TImpl[numberOfBuffers];

                if (typeof(TImpl) != typeof(ArrayMemory<T>) && _factory == null)
                {
                    ThrowArgumentNull<Func<int, TImpl>>();
                }

                _factory = factory;
                _bufferLength = bufferLength;
                _poolId = poolId;
            }

            /// <summary>Gets an ID for the bucket to use with events.</summary>
            internal int Id => GetHashCode();

            [MethodImpl(MethodImplOptions.NoInlining)]
            internal TImpl CreateNew()
            {
                if (typeof(TImpl) == typeof(ArrayMemory<T>) && _factory == null)
                {
                    if (_bufferLength <= 64 * 1024)
                    {
                        if (_sliceBucket == null)
                        {
                            _sliceBucket = new ArrayMemorySliceBucket<T>(_bufferLength, _buffers.Length);
                        }

                        var slice = _sliceBucket.RentMemory();
                        Debug.Assert(slice.Length == _bufferLength);
                        var asTImpl = Unsafe.As<TImpl>(slice);
                        return asTImpl;
                    }

                    return Unsafe.As<TImpl>(ArrayMemory<T>.Create(_bufferLength));
                }
                // ReSharper disable once PossibleNullReferenceException
                return _factory.Invoke(_bufferLength);
            }

            /// <summary>Takes an array from the bucket.  If the bucket is empty, returns null.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TImpl Rent()
            {
                TImpl[] buffers = _buffers;
                TImpl buffer = null;

                // While holding the lock, grab whatever is at the next available index and
                // update the index.  We do as little work as possible while holding the spin
                // lock to minimize contention with other threads.  The try/finally is
                // necessary to properly handle thread aborts on platforms which have them.
                bool lockTaken = false, allocateBuffer = false;
#if !NETCOREAPP
                try
#endif
                {
                    _lock.Enter(ref lockTaken);

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
                    if (lockTaken) _lock.Exit(false);
                }

                // While we were holding the lock, we grabbed whatever was at the next available index, if
                // there was one.  If we tried and if we got back null, that means we hadn't yet allocated
                // for that slot, in which case we should do so now.
                if (allocateBuffer)
                {
                    buffer = CreateNew();

                    var log = RetainedMemoryPoolEventSource.Log;
                    if (log.IsEnabled())
                    {
                        log.BufferAllocated(buffer.GetHashCode(), _bufferLength, _poolId, Id,
                            RetainedMemoryPoolEventSource.BufferAllocatedReason.Pooled);
                    }
                }

                return buffer;
            }

            /// <summary>
            /// Attempts to return the buffer to the bucket.  If successful, the buffer will be stored
            /// in the bucket and true will be returned; otherwise, the buffer won't be stored, and false
            /// will be returned.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool Return(TImpl memory)
            {
                // Check to see if the buffer is the correct size for this bucket
                if (memory.Length != _bufferLength)
                {
                    ThrowNotFromPool<TImpl>();
                }

                // While holding the spin lock, if there's room available in the bucket,
                // put the buffer into the next available slot.  Otherwise, we just drop it.
                // The try/finally is necessary to properly handle thread aborts on platforms
                // which have them.
                bool lockTaken = false;
                bool dispose;
#if !NETCOREAPP
                try
#endif
                {
                    _lock.Enter(ref lockTaken);
                    dispose = _index == 0;
                    if (!dispose)
                    {
                        _buffers[--_index] = memory;
                    }
                }
#if !NETCOREAPP
                finally
#endif
                {
                    if (lockTaken) _lock.Exit(false);
                }

                if (dispose)
                {
                    ((IDisposable)memory).Dispose();
                }

                return !dispose;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SelectBucketIndex(int bufferSize)
        {
            Debug.Assert(bufferSize >= 0);

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

            return poolIndex + (int)bitsRemaining;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetMaxSizeForBucket(int binIndex)
        {
            int maxSize = _minBufferLength << binIndex;
            Debug.Assert(maxSize >= 0);
            return maxSize;
        }

        [EventSource(Guid = "C5BB9D49-21E4-4339-B6BC-981486D123DB", Name = "Spreads.Buffers.MemoryManagerPoolEventSource")]
        internal sealed class RetainedMemoryPoolEventSource : EventSource
        {
            internal static readonly RetainedMemoryPoolEventSource Log = new RetainedMemoryPoolEventSource();

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

            private RetainedMemoryPoolEventSource() : base(
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
}
