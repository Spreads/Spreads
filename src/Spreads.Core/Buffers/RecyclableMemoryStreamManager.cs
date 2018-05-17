// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ---------------------------------------------------------------------
// Copyright (c) 2015 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// ---------------------------------------------------------------------

using Spreads.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Buffers
{
    /// <summary>
    /// Manages pools of RecyclableMemoryStream objects.
    /// </summary>
    /// <remarks>
    /// There are two pools managed in here. The small pool contains same-sized buffers that are handed to streams
    /// as they write more data.
    ///
    /// For scenarios that need to call GetBuffer(), the large pool contains buffers of various sizes, all
    /// multiples of LargeBufferMultiple (1 MB by default). They are split by size to avoid overly-wasteful buffer
    /// usage. There should be far fewer 8 MB buffers than 1 MB buffers, for example.
    /// </remarks>
    public partial class RecyclableMemoryStreamManager
    {
        public static RecyclableMemoryStreamManager Default = new RecyclableMemoryStreamManager(8 * 1024, DefaultLargeBufferMultiple, DefaultMaximumBufferSize);

        /// <summary>
        /// Generic delegate for handling events without any arguments.
        /// </summary>
        public delegate void EventHandler();

        /// <summary>
        /// Delegate for handling large buffer discard reports.
        /// </summary>
        /// <param name="reason">Reason the buffer was discarded.</param>
        public delegate void LargeBufferDiscardedEventHandler(Events.MemoryStreamDiscardReason reason);

        /// <summary>
        /// Delegate for handling reports of stream size when streams are allocated
        /// </summary>
        /// <param name="bytes">Bytes allocated.</param>
        public delegate void StreamLengthReportHandler(long bytes);

        /// <summary>
        /// Delegate for handling periodic reporting of memory use statistics.
        /// </summary>
        /// <param name="smallPoolInUseBytes">Bytes currently in use in the small pool.</param>
        /// <param name="smallPoolFreeBytes">Bytes currently free in the small pool.</param>
        /// <param name="largePoolInUseBytes">Bytes currently in use in the large pool.</param>
        /// <param name="largePoolFreeBytes">Bytes currently free in the large pool.</param>
        public delegate void UsageReportEventHandler(
            long smallPoolInUseBytes, long smallPoolFreeBytes, long largePoolInUseBytes, long largePoolFreeBytes);

        public const int DefaultBlockSize = 128 * 1024;
        public const int DefaultLargeBufferMultiple = 1024 * 1024;
        public const int DefaultMaximumBufferSize = 128 * 1024 * 1024;

        private readonly int _blockSize;
        private readonly long[] _largeBufferFreeSize;
        private readonly long[] _largeBufferInUseSize;

        private readonly int _largeBufferMultiple;

        /// <summary>
        /// pools[0] = 1x largeBufferMultiple buffers
        /// pools[1] = 2x largeBufferMultiple buffers
        /// etc., up to maximumBufferSize
        /// </summary>
        private readonly ConcurrentStack<byte[]>[] _largePools;

        private readonly int _maximumBufferSize;

        /// <summary>
        /// Initializes the memory manager with the default block/buffer specifications.
        /// </summary>
        public RecyclableMemoryStreamManager()
            : this(DefaultBlockSize, DefaultLargeBufferMultiple, DefaultMaximumBufferSize) { }

        /// <summary>
        /// Initializes the memory manager with the given block requiredSize.
        /// </summary>
        /// <param name="blockSize">Size of each block that is pooled. Must be > 0. Will use the next power of two if the given size in not a power of two.</param>
        /// <param name="largeBufferMultiple">Each large buffer will be a multiple of this value.</param>
        /// <param name="maximumBufferSize">Buffers larger than this are not pooled</param>
        /// <exception cref="ArgumentOutOfRangeException">blockSize is not a positive number, or largeBufferMultiple is not a positive number, or maximumBufferSize is less than blockSize.</exception>
        /// <exception cref="ArgumentException">maximumBufferSize is not a multiple of largeBufferMultiple</exception>
        public RecyclableMemoryStreamManager(int blockSize, int largeBufferMultiple, int maximumBufferSize)
        {
            if (blockSize <= 0)
            {
                throw new ArgumentOutOfRangeException("blockSize", blockSize, "blockSize must be a positive number");
            }

            if (largeBufferMultiple <= 0)
            {
                throw new ArgumentOutOfRangeException("largeBufferMultiple",
                                                      "largeBufferMultiple must be a positive number");
            }

            if (maximumBufferSize < blockSize)
            {
                throw new ArgumentOutOfRangeException("maximumBufferSize",
                                                      "maximumBufferSize must be at least blockSize");
            }

            _blockSize = BitUtil.FindNextPositivePowerOfTwo(blockSize);
            _largeBufferMultiple = largeBufferMultiple;
            _maximumBufferSize = maximumBufferSize;

            if (!IsLargeBufferMultiple(maximumBufferSize))
            {
                throw new ArgumentException("maximumBufferSize is not a multiple of largeBufferMultiple",
                                            "maximumBufferSize");
            }

            //_smallPool = new ConcurrentBag<byte[]>();
            var numLargePools = maximumBufferSize / largeBufferMultiple;

            // +1 to store size of bytes in use that are too large to be pooled
            _largeBufferInUseSize = new long[numLargePools + 1];
            _largeBufferFreeSize = new long[numLargePools];

            _largePools = new ConcurrentStack<byte[]>[numLargePools];

            for (var i = 0; i < _largePools.Length; ++i)
            {
                _largePools[i] = new ConcurrentStack<byte[]>();
            }

            Events.Write.MemoryStreamManagerInitialized(blockSize, largeBufferMultiple, maximumBufferSize);
        }

        /// <summary>
        /// The size of each block. It must be set at creation and cannot be changed.
        /// </summary>
        public int BlockSize => _blockSize;

        /// <summary>
        /// All buffers are multiples of this number. It must be set at creation and cannot be changed.
        /// </summary>
        public int LargeBufferMultiple => _largeBufferMultiple;

        /// <summary>
        /// Gets or sets the maximum buffer size.
        /// </summary>
        /// <remarks>Any buffer that is returned to the pool that is larger than this will be
        /// discarded and garbage collected.</remarks>
        public int MaximumBufferSize => _maximumBufferSize;

        /// <summary>
        /// Number of bytes in large pool not currently in use
        /// </summary>
        public long LargePoolFreeSize => _largeBufferFreeSize.Sum();

        /// <summary>
        /// Number of bytes currently in use by streams from the large pool
        /// </summary>
        public long LargePoolInUseSize => _largeBufferInUseSize.Sum();

        /// <summary>
        /// How many buffers are in the large pool
        /// </summary>
        public long LargeBuffersFree
        {
            get
            {
                long free = 0;
                foreach (var pool in _largePools)
                {
                    free += pool.Count;
                }
                return free;
            }
        }

        /// <summary>
        /// How many bytes of small free blocks to allow before we start dropping
        /// those returned to us.
        /// </summary>
        //public long MaximumFreeSmallPoolBytes { get; set; }

        /// <summary>
        /// How many bytes of large free buffers to allow before we start dropping
        /// those returned to us.
        /// </summary>
        public long MaximumFreeLargePoolBytes { get; set; }

        /// <summary>
        /// Maximum stream capacity in bytes. Attempts to set a larger capacity will
        /// result in an exception.
        /// </summary>
        /// <remarks>A value of 0 indicates no limit.</remarks>
        public long MaximumStreamCapacity { get; set; }

        /// <summary>
        /// Whether to save callstacks for stream allocations. This can help in debugging.
        /// It should NEVER be turned on generally in production.
        /// </summary>
        public bool GenerateCallStacks { get; set; }

        /// <summary>
        /// Whether dirty buffers can be immediately returned to the buffer pool. E.g. when GetBuffer() is called on
        /// a stream and creates a single large buffer, if this setting is enabled, the other blocks will be returned
        /// to the buffer pool immediately.
        /// Note when enabling this setting that the user is responsible for ensuring that any buffer previously
        /// retrieved from a stream which is subsequently modified is not used after modification (as it may no longer
        /// be valid).
        /// </summary>
        public bool AggressiveBufferReturn { get; set; }

        /// <summary>
        /// Removes and returns a single block from the pool.
        /// </summary>
        /// <returns>A byte[] array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte[] GetBlock()
        {
            var buffer = BufferPool<byte>.Rent(_blockSize);
            if (buffer.Length == _blockSize) return buffer;
            BufferPool<byte>.Return(buffer, false);
            buffer = new byte[_blockSize];
            return buffer;
        }

        /// <summary>
        /// Returns a buffer of arbitrary size from the large buffer pool. This buffer
        /// will be at least the requiredSize and always be a multiple of largeBufferMultiple.
        /// </summary>
        /// <param name="requiredSize">The minimum length of the buffer</param>
        /// <param name="tag">The tag of the stream returning this buffer, for logging if necessary.</param>
        /// <returns>A buffer of at least the required size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte[] GetLargeBuffer(int requiredSize, string tag)
        {
            requiredSize = RoundToLargeBufferMultiple(requiredSize);

            var poolIndex = requiredSize / _largeBufferMultiple - 1;

            byte[] buffer;
            if (poolIndex < _largePools.Length)
            {
                if (!_largePools[poolIndex].TryPop(out buffer))
                {
                    buffer = new byte[requiredSize];

                    Events.Write.MemoryStreamNewLargeBufferCreated(requiredSize, LargePoolInUseSize);
                    ReportLargeBufferCreated();
                }
                else
                {
                    Interlocked.Add(ref _largeBufferFreeSize[poolIndex], -buffer.Length);
                }
            }
            else
            {
                // Memory is too large to pool. They get a new buffer.

                // We still want to track the size, though, and we've reserved a slot
                // in the end of the inuse array for nonpooled bytes in use.
                poolIndex = _largeBufferInUseSize.Length - 1;

                // We still want to round up to reduce heap fragmentation.
                buffer = new byte[requiredSize];
                string callStack = null;
                if (GenerateCallStacks)
                {
                    // Grab the stack -- we want to know who requires such large buffers
                    callStack = Environment.StackTrace;
                }
                Events.Write.MemoryStreamNonPooledLargeBufferCreated(requiredSize, tag, callStack);
                ReportLargeBufferCreated();
            }

            Interlocked.Add(ref _largeBufferInUseSize[poolIndex], buffer.Length);

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RoundToLargeBufferMultiple(int requiredSize)
        {
            return ((requiredSize + LargeBufferMultiple - 1) / LargeBufferMultiple) * LargeBufferMultiple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsLargeBufferMultiple(int value)
        {
            return (value != 0) && (value % LargeBufferMultiple) == 0;
        }

        /// <summary>
        /// Returns the buffer to the large pool
        /// </summary>
        /// <param name="buffer">The buffer to return.</param>
        /// <param name="tag">The tag of the stream returning this buffer, for logging if necessary.</param>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentException">buffer.Length is not a multiple of LargeBufferMultiple (it did not originate from this pool)</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReturnLargeBuffer(byte[] buffer, string tag)
        {
            if (buffer == null)
            {
                ThrowHelper.ThrowArgumentNullException("buffer");
                return;
            }

            if (!IsLargeBufferMultiple(buffer.Length))
            {
                ThrowHelper.ThrowArgumentException(
                    "buffer did not originate from this memory manager. The size is not a multiple of " +
                    LargeBufferMultiple);
            }

            var poolIndex = buffer.Length / _largeBufferMultiple - 1;

            if (poolIndex < _largePools.Length)
            {
                if ((_largePools[poolIndex].Count + 1) * buffer.Length <= MaximumFreeLargePoolBytes ||
                    MaximumFreeLargePoolBytes == 0)
                {
                    _largePools[poolIndex].Push(buffer);
                    Interlocked.Add(ref _largeBufferFreeSize[poolIndex], buffer.Length);
                }
                else
                {
                    Events.Write.MemoryStreamDiscardBuffer(Events.MemoryStreamBufferType.Large, tag,
                                                           Events.MemoryStreamDiscardReason.EnoughFree);
                    ReportLargeBufferDiscarded(Events.MemoryStreamDiscardReason.EnoughFree);
                }
            }
            else
            {
                // This is a non-poolable buffer, but we still want to track its size for inuse
                // analysis. We have space in the inuse array for this.
                poolIndex = _largeBufferInUseSize.Length - 1;

                Events.Write.MemoryStreamDiscardBuffer(Events.MemoryStreamBufferType.Large, tag,
                                                       Events.MemoryStreamDiscardReason.TooLarge);
                ReportLargeBufferDiscarded(Events.MemoryStreamDiscardReason.TooLarge);
            }

            Interlocked.Add(ref _largeBufferInUseSize[poolIndex], -buffer.Length);

            //ReportUsageReport(_smallPoolInUseSize, _smallPoolFreeSize, LargePoolInUseSize, LargePoolFreeSize);
            ReportUsageReport(0, 0, LargePoolInUseSize, LargePoolFreeSize);
        }

        /// <summary>
        /// Returns the blocks to the pool
        /// </summary>
        /// <param name="blocks">Collection of blocks to return to the pool</param>
        /// <param name="tag">The tag of the stream returning these blocks, for logging if necessary.</param>
        /// <exception cref="ArgumentNullException">blocks is null</exception>
        /// <exception cref="ArgumentException">blocks contains buffers that are the wrong size (or null) for this memory manager</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReturnBlocks(List<byte[]> blocks, string tag)
        {
            if (blocks == null)
            {
                ThrowHelper.ThrowArgumentNullException("blocks");
                return;
            }

            foreach (var block in blocks)
            {
                if (block == null || block.Length != BlockSize)
                {
                    ThrowHelper.ThrowArgumentException("blocks contains buffers that are not BlockSize in length");
                }
            }

            foreach (var block in blocks)
            {
                BufferPool<byte>.Return(block, true);
            }

            ReportUsageReport(0, 0, LargePoolInUseSize, LargePoolFreeSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportBlockCreated()
        {
            var blockCreated = Interlocked.CompareExchange(ref BlockCreated, null, null);
            blockCreated?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportBlockDiscarded()
        {
            var blockDiscarded = Interlocked.CompareExchange(ref BlockDiscarded, null, null);
            blockDiscarded?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportLargeBufferCreated()
        {
            var largeBufferCreated = Interlocked.CompareExchange(ref LargeBufferCreated, null, null);
            largeBufferCreated?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportLargeBufferDiscarded(Events.MemoryStreamDiscardReason reason)
        {
            var largeBufferDiscarded = Interlocked.CompareExchange(ref LargeBufferDiscarded, null, null);
            largeBufferDiscarded?.Invoke(reason);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportStreamCreated()
        {
            var streamCreated = Interlocked.CompareExchange(ref StreamCreated, null, null);
            streamCreated?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportStreamDisposed()
        {
            var streamDisposed = Interlocked.CompareExchange(ref StreamDisposed, null, null);
            streamDisposed?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportStreamFinalized()
        {
            var streamFinalized = Interlocked.CompareExchange(ref StreamFinalized, null, null);
            streamFinalized?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportStreamLength(long bytes)
        {
            var streamLength = Interlocked.CompareExchange(ref StreamLength, null, null);
            streamLength?.Invoke(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportStreamToArray()
        {
            var streamConvertedToArray = Interlocked.CompareExchange(ref StreamConvertedToArray, null, null);
            streamConvertedToArray?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("TRACE_RMS")]
        internal void ReportUsageReport(
            long smallPoolInUseBytes, long smallPoolFreeBytes, long largePoolInUseBytes, long largePoolFreeBytes)
        {
            var usageReport = Interlocked.CompareExchange(ref UsageReport, null, null);
            usageReport?.Invoke(smallPoolInUseBytes, smallPoolFreeBytes, largePoolInUseBytes, largePoolFreeBytes);
        }

        /// <summary>
        /// Retrieve a new MemoryStream object with no tag and a default initial capacity.
        /// </summary>
        /// <returns>A MemoryStream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RecyclableMemoryStream GetStream()
        {
            return RecyclableMemoryStream.Create(this);
        }

        /// <summary>
        /// Retrieve a new MemoryStream object with the given tag and a default initial capacity.
        /// </summary>
        /// <param name="tag">A tag which can be used to track the source of the stream.</param>
        /// <returns>A MemoryStream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RecyclableMemoryStream GetStream(string tag)
        {
            return RecyclableMemoryStream.Create(this, tag);
        }

        /// <summary>
        /// Retrieve a new MemoryStream object with the given tag and at least the given capacity.
        /// </summary>
        /// <param name="tag">A tag which can be used to track the source of the stream.</param>
        /// <param name="requiredSize">The minimum desired capacity for the stream.</param>
        /// <returns>A MemoryStream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RecyclableMemoryStream GetStream(string tag, int requiredSize)
        {
            return RecyclableMemoryStream.Create(this, tag, requiredSize);
        }

        /// <summary>
        /// Retrieve a new MemoryStream object with the given tag and at least the given capacity, possibly using
        /// a single continugous underlying buffer.
        /// </summary>
        /// <remarks>Retrieving a MemoryStream which provides a single contiguous buffer can be useful in situations
        /// where the initial size is known and it is desirable to avoid copying data between the smaller underlying
        /// buffers to a single large one. This is most helpful when you know that you will always call GetBuffer
        /// on the underlying stream.</remarks>
        /// <param name="tag">A tag which can be used to track the source of the stream.</param>
        /// <param name="requiredSize">The minimum desired capacity for the stream.</param>
        /// <param name="asContiguousBuffer">Whether to attempt to use a single contiguous buffer.</param>
        /// <returns>A MemoryStream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RecyclableMemoryStream GetStream(string tag, int requiredSize, bool asContiguousBuffer)
        {
            if (!asContiguousBuffer || requiredSize <= BlockSize)
            {
                return GetStream(tag, requiredSize);
            }

            return RecyclableMemoryStream.Create(this, tag, requiredSize, GetLargeBuffer(requiredSize, tag));
        }

        /// <summary>
        /// Retrieve a new MemoryStream object with the given tag and with contents copied from the provided
        /// buffer. The provided buffer is not wrapped or used after construction.
        /// </summary>
        /// <remarks>The new stream's position is set to the beginning of the stream when returned.</remarks>
        /// <param name="tag">A tag which can be used to track the source of the stream.</param>
        /// <param name="buffer">The byte buffer to copy data from.</param>
        /// <param name="offset">The offset from the start of the buffer to copy from.</param>
        /// <param name="count">The number of bytes to copy from the buffer.</param>
        /// <returns>A MemoryStream.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public RecyclableMemoryStream GetStream(string tag, byte[] buffer, int offset, int count)
        {
            var stream = RecyclableMemoryStream.Create(this, tag, count);
            stream.Write(buffer, offset, count);
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Triggered when a new block is created.
        /// </summary>
        public event EventHandler BlockCreated;

        /// <summary>
        /// Triggered when a new block is created.
        /// </summary>
        public event EventHandler BlockDiscarded;

        /// <summary>
        /// Triggered when a new large buffer is created.
        /// </summary>
        public event EventHandler LargeBufferCreated;

        /// <summary>
        /// Triggered when a new stream is created.
        /// </summary>
        public event EventHandler StreamCreated;

        /// <summary>
        /// Triggered when a stream is disposed.
        /// </summary>
        public event EventHandler StreamDisposed;

        /// <summary>
        /// Triggered when a stream is finalized.
        /// </summary>
        public event EventHandler StreamFinalized;

        /// <summary>
        /// Triggered when a stream is finalized.
        /// </summary>
        public event StreamLengthReportHandler StreamLength;

        /// <summary>
        /// Triggered when a user converts a stream to array.
        /// </summary>
        public event EventHandler StreamConvertedToArray;

        /// <summary>
        /// Triggered when a large buffer is discarded, along with the reason for the discard.
        /// </summary>
        public event LargeBufferDiscardedEventHandler LargeBufferDiscarded;

        /// <summary>
        /// Periodically triggered to report usage statistics.
        /// </summary>
        public event UsageReportEventHandler UsageReport;
    }
}