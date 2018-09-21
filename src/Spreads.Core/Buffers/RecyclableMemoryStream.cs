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

using Spreads.Collections.Concurrent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Buffers
{
    using Events = RecyclableMemoryStreamManager.Events;

    /// <summary>
    /// MemoryStream implementation that deals with pooling and managing memory streams which use potentially large
    /// buffers.
    /// </summary>
    /// <remarks>
    /// This class works in tandem with the RecylableMemoryStreamManager to supply MemoryStream
    /// objects to callers, while avoiding these specific problems:
    /// 1. LOH allocations - since all large buffers are pooled, they will never incur a Gen2 GC
    /// 2. Memory waste - A standard memory stream doubles its size when it runs out of room. This
    /// leads to continual memory growth as each stream approaches the maximum allowed size.
    /// 3. Memory copying - Each time a MemoryStream grows, all the bytes are copied into new buffers.
    /// This implementation only copies the bytes when GetBuffer is called.
    /// 4. Memory fragmentation - By using homogeneous buffer sizes, it ensures that blocks of memory
    /// can be easily reused.
    ///
    /// The stream is implemented on top of a series of uniformly-sized blocks. As the stream's length grows,
    /// additional blocks are retrieved from the memory manager. It is these blocks that are pooled, not the stream
    /// object itself.
    ///
    /// The biggest wrinkle in this implementation is when GetBuffer() is called. This requires a single
    /// contiguous buffer. If only a single block is in use, then that block is returned. If multiple blocks
    /// are in use, we retrieve a larger buffer from the memory manager. These large buffers are also pooled,
    /// split by size--they are multiples of a chunk size (1 MB by default).
    ///
    /// Once a large buffer is assigned to the stream the blocks are NEVER again used for this stream. All operations take place on the
    /// large buffer. The large buffer can be replaced by a larger buffer from the pool as needed. All blocks and large buffers
    /// are maintained in the stream until the stream is disposed (unless AggressiveBufferReturn is enabled in the stream manager).
    ///
    /// </remarks>
    public sealed class RecyclableMemoryStream : MemoryStream
    {
        private static readonly ObjectPool<RecyclableMemoryStream> Pool = new ObjectPool<RecyclableMemoryStream>(() => new RecyclableMemoryStream(), Environment.ProcessorCount * 16);

        private const long MaxStreamLength = int.MaxValue;

        private static readonly byte[] EmptyArray = Array.Empty<byte>();

        /// <summary>
        /// All of these blocks must be the same size
        /// </summary>
        private readonly List<byte[]> _blocks = new List<byte[]>(1);

        /// <summary>
        /// This is only set by GetBuffer() if the necessary buffer is larger than a single block size, or on
        /// construction if the caller immediately requests a single large buffer.
        /// </summary>
        /// <remarks>If this field is non-null, it contains the concatenation of the bytes found in the individual
        /// blocks. Once it is created, this (or a larger) largeBuffer will be used for the life of the stream.
        /// </remarks>
        private RetainedMemory<byte> _largeBuffer;

#if DEBUG
        private static long _lastId;
        private long _id;
        private string _tag;
#endif

        /// <summary>
        /// Unique identifier for this stream across it's entire lifetime
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal long Id
        {
            get
            {
                CheckDisposed();
#if DEBUG
                return _id;
#else
                return 0;
#endif
            }
        }

        /// <summary>
        /// A temporary identifier for the current usage of this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal string Tag
        {
            get
            {
                CheckDisposed();
#if DEBUG
                return _tag;
#else
                return null;
#endif
            }
        }

        private RecyclableMemoryStreamManager _memoryManager;

        /// <summary>
        /// Gets the memory manager being used by this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal RecyclableMemoryStreamManager MemoryManager
        {
            get
            {
                CheckDisposed();
                return _memoryManager;
            }
        }

        private long _refCount;

        /// <summary>
        /// Callstack of the constructor. It is only set if MemoryManager.GenerateCallStacks is true,
        /// which should only be in debugging situations.
        /// </summary>
        internal string AllocationStack { get; private set; }

        /// <summary>
        /// Callstack of the Dispose call. It is only set if MemoryManager.GenerateCallStacks is true,
        /// which should only be in debugging situations.
        /// </summary>
        internal string DisposeStack { get; private set; }

        #region Constructors

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RecyclableMemoryStream Create(int requestedSize)
        {
            return Create(requestedSize, default(RetainedMemory<byte>), -1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RecyclableMemoryStream Create(RetainedMemory<byte> initialLargeBuffer)
        {
            return Create(0, initialLargeBuffer, initialLargeBuffer.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RecyclableMemoryStream Create(Memory<byte> initialLargeBuffer)
        {
            var rm = new RetainedMemory<byte>(initialLargeBuffer, default);
            return Create(0, rm, rm.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RecyclableMemoryStream Create(RetainedMemory<byte> initialLargeBuffer, int length)
        {
            if ((uint) length > (uint) initialLargeBuffer.Length)
            {
                ThrowHelper.ThrowArgumentException(nameof(length));
            }
            return Create(0, initialLargeBuffer, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RecyclableMemoryStream Create(int requestedSize,
            RetainedMemory<byte> initialLargeBuffer, int length = -1, string tag = null,
            RecyclableMemoryStreamManager memoryManager = null)
        {
            memoryManager = memoryManager ?? RecyclableMemoryStreamManager.Default;
            var rms = Pool.Allocate();
            rms._refCount = 1;
            rms._memoryManager = memoryManager;
#if DEBUG
            rms._id = Interlocked.Increment(ref _lastId);
            rms._tag = tag;
#endif
            if (requestedSize < memoryManager.BlockSize)
            {
                requestedSize = memoryManager.BlockSize;
            }

            if (initialLargeBuffer.IsEmpty)
            {
                rms.EnsureCapacity(requestedSize);
            }
            else
            {
                rms._largeBuffer = initialLargeBuffer;
                if (length > 0)
                {
                    if (length > initialLargeBuffer.Length)
                    {
                        ThrowHelper.ThrowArgumentException("length is larger than buffer size");
                    }
                    rms._length = length;
                }
            }

#if DEBUG
            if (rms._memoryManager.GenerateCallStacks)
            {
                rms.AllocationStack = Environment.StackTrace;
            }
            Events.Write.MemoryStreamCreated(rms._id, rms._tag, requestedSize);
#endif
            rms._memoryManager.ReportStreamCreated();

            return rms;
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="requestedSize">The initial requested size to prevent future allocations</param>
        /// <param name="initialLargeBuffer">An initial buffer to use. This buffer will be owned by the stream and returned to the memory manager upon Dispose.</param>
        /// <param name="length">Set length if initialLargeBuffer has data.</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        /// <param name="memoryManager">The memory manager</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RecyclableMemoryStream Create(int requestedSize, byte[] initialLargeBuffer, int length = -1, string tag = null, RecyclableMemoryStreamManager memoryManager = null)
        {
            return Create(requestedSize, initialLargeBuffer == null ? default : new RetainedMemory<byte>(initialLargeBuffer), length, tag, memoryManager);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RecyclableMemoryStream() : base(EmptyArray)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecyclableMemoryStream Create()
        {
            return Create(0, default(RetainedMemory<byte>), -1, null, null);
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object.
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecyclableMemoryStream Create(RecyclableMemoryStreamManager memoryManager)
        {
            return Create(0, null, -1, null, memoryManager);
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecyclableMemoryStream Create(RecyclableMemoryStreamManager memoryManager, string tag)
        {
            return Create(0, null, -1, tag, memoryManager);
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="requestedSize">The initial requested size to prevent future allocations</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        /// <param name="memoryManager">The memory manager</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecyclableMemoryStream Create(int requestedSize, string tag,
            RecyclableMemoryStreamManager memoryManager)
        {
            return Create(requestedSize, null, -1, tag, memoryManager);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long AddReference()
        {
            return Interlocked.Increment(ref _refCount);
        }

#endregion Constructors

#region Dispose and Finalize

        ~RecyclableMemoryStream()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the memory used by this stream back to the pool.
        /// </summary>
        /// <param name="disposing">Whether we're disposing (true), or being called by the finalizer (false)</param>
        /// <remarks>This method is not thread safe and it may not be called more than once.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1816:CallGCSuppressFinalizeCorrectly", Justification = "We have different disposal semantics, so SuppressFinalize is in a different spot.")]
        protected override void Dispose(bool disposing)
        {
            var remaining = Interlocked.Decrement(ref _refCount);

            if (disposing && remaining > 0)
            {
                return;
            }

            if (remaining < 0)
            {
                string doubleDisposeStack = null;
                if (_memoryManager.GenerateCallStacks)
                {
                    doubleDisposeStack = Environment.StackTrace;
                }
#if DEBUG
                Events.Write.MemoryStreamDoubleDispose(_id, _tag, AllocationStack, DisposeStack,
                    doubleDisposeStack);
#endif
                return;
            }

#if DEBUG
            Events.Write.MemoryStreamDisposed(_id, _tag);
            if (_memoryManager.GenerateCallStacks)
            {
                DisposeStack = Environment.StackTrace;
            }
#endif
            if (disposing)
            {
                _memoryManager.ReportStreamDisposed();

                // regardless of Free result below we do not need finalization, we have done cleaning
                GC.SuppressFinalize(this);
            }
            else
            {
                // We're being finalized.
#if DEBUG
                Events.Write.MemoryStreamFinalized(_id, _tag, AllocationStack);
#endif
                if (AppDomain.CurrentDomain.IsFinalizingForUnload())
                {
                    // If we're being finalized because of a shutdown, don't go any further.
                    // We have no idea what's already been cleaned up. Triggering events may cause
                    // a crash.
                    base.Dispose(false);
                    return;
                }
                _memoryManager.ReportStreamFinalized();
            }

            _memoryManager.ReportStreamLength(_length);

            if (!_largeBuffer.IsEmpty)
            {
                _largeBuffer.Dispose();
                _largeBuffer = default;
            }
#if DEBUG
            _memoryManager.ReturnBlocks(_blocks, _tag);
#else
            _memoryManager.ReturnBlocks(_blocks, null);
#endif
            _blocks.Clear();

            _length = 0;
            _position = 0;
#if DEBUG
            _id = 0;
            _tag = null;
#endif
            _memoryManager = null;

            base.Dispose(disposing);

            // last operation, prevent race condition (had it with _memoryManager = null when buffer was reused before Dispose finished)
            Pool.Free(this);
        }

        /// <summary>
        /// Equivalent to Dispose
        /// </summary>
        public override void Close()
        {
            Dispose(true);
        }

#endregion Dispose and Finalize

#region MemoryStream overrides

        /// <summary>
        /// Gets or sets the capacity
        /// </summary>
        /// <remarks>Capacity is always in multiples of the memory manager's block size, unless
        /// the large buffer is in use.  Capacity never decreases during a stream's lifetime.
        /// Explicitly setting the capacity to a lower value than the current value will have no effect.
        /// This is because the buffers are all pooled by chunks and there's little reason to
        /// allow stream truncation.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override int Capacity
        {
            get
            {
                CheckDisposed();
                return CapacityInternal;
            }
            set
            {
                CheckDisposed();
                EnsureCapacity(value);
            }
        }

        internal int CapacityInternal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_largeBuffer.IsEmpty)
                {
                    return _largeBuffer.Length;
                }

                if (_blocks.Count > 0)
                {
                    return _blocks.Count * _memoryManager.BlockSize;
                }
                return 0;
            }
        }

        private long _length;

        /// <summary>
        /// Gets the number of bytes written to this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override long Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return _length;
            }
        }

        internal long LengthInternal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _length;
            }
        }

        private long _position;

        /// <summary>
        /// Gets the current position in the stream
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return _position;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckDisposed();
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value", "value must be non-negative");
                }

                if (value > MaxStreamLength)
                {
                    throw new ArgumentOutOfRangeException("value", "value cannot be more than " + MaxStreamLength);
                }

                _position = (int)value;
            }
        }

        internal long PositionInternal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _position;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _position = (int)value;
            }
        }

        /// <summary>
        /// Whether the stream can currently read
        /// </summary>
        public override bool CanRead => Interlocked.Read(ref _refCount) != 0;

        /// <summary>
        /// Whether the stream can currently seek
        /// </summary>
        public override bool CanSeek => Interlocked.Read(ref _refCount) != 0;

        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanTimeout => false;

        /// <summary>
        /// Whether the stream can currently write
        /// </summary>
        public override bool CanWrite => Interlocked.Read(ref _refCount) != 0;

        /// <summary>
        /// Returns a single buffer containing the contents of the stream.
        /// The buffer may be longer than the stream length.
        /// </summary>
        /// <returns>A byte[] buffer</returns>
        /// <remarks>IMPORTANT: Doing a Write() after calling GetBuffer() invalidates the buffer. The old buffer is held onto
        /// until Dispose is called, but the next time GetBuffer() is called, a new buffer from the pool will be required.</remarks>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override byte[] GetBuffer()
        {
            throw new NotSupportedException("Use GetMemory");
        }

        public Memory<byte> GetMemory()
        {
            CheckDisposed();

            if (!_largeBuffer.IsEmpty)
            {
                return _largeBuffer.Memory.Slice(0, checked((int)_length));
            }

            if (_blocks.Count == 1)
            {
                return _blocks[0];
            }

            // Memory needs to reflect the capacity, not the length, because
            // it's possible that people will manipulate the buffer directly
            // and set the length afterward. Capacity sets the expectation
            // for the size of the buffer.
#if DEBUG
            var newBuffer = _memoryManager.GetLargeBuffer(Capacity, _tag);
#else
            var newBuffer = _memoryManager.GetLargeBuffer(Capacity, null);
#endif

            // InternalRead will check for existence of largeBuffer, so make sure we
            // don't set it until after we've copied the data.
            InternalRead(newBuffer, 0, _length, 0);
            _largeBuffer = OwnedPooledArray<byte>.Create(newBuffer).Retain();

            if (_blocks.Count > 0)
            {
#if DEBUG
                _memoryManager.ReturnBlocks(_blocks, _tag);
#else
                _memoryManager.ReturnBlocks(_blocks, null);
#endif
                _blocks.Clear();
            }

            return _largeBuffer.Memory.Slice(0, checked((int)_length));
        }

        /// <summary>
        /// Returns a new array with a copy of the buffer's contents. You should almost certainly be using GetBuffer combined with the Length to
        /// access the bytes in this stream. Calling ToArray will destroy the benefits of pooled buffers, but it is included
        /// for the sake of completeness.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        [Obsolete("This method has degraded performance vs. GetBuffer and should be avoided.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override byte[] ToArray()
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        {
            CheckDisposed();
            var newBuffer = new byte[Length];

            InternalRead(newBuffer, 0, _length, 0);
            var stack = _memoryManager.GenerateCallStacks ? Environment.StackTrace : null;
#if DEBUG
            Events.Write.MemoryStreamToArray(_id, _tag, stack, 0);
#else
            Events.Write.MemoryStreamToArray(0, null, stack, 0);
#endif
            _memoryManager.ReportStreamToArray();

            return newBuffer;
        }

        /// <summary>
        /// Reads from the current position into the provided buffer
        /// </summary>
        /// <param name="buffer">Destination buffer</param>
        /// <param name="offset">Offset into buffer at which to start placing the read bytes.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>The number of bytes read</returns>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is less than 0</exception>
        /// <exception cref="ArgumentException">offset subtracted from the buffer length is less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return checked((int)SafeRead(buffer, offset, count, ref _position));
        }

        /// <summary>
        /// Reads from the specified position into the provided buffer
        /// </summary>
        /// <param name="buffer">Destination buffer</param>
        /// <param name="offset">Offset into buffer at which to start placing the read bytes.</param>
        /// <param name="count">Number of bytes to read.</param>
        /// <param name="streamPosition">Position in the stream to start reading from</param>
        /// <returns>The number of bytes read</returns>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is less than 0</exception>
        /// <exception cref="ArgumentException">offset subtracted from the buffer length is less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long SafeRead(byte[] buffer, long offset, long count, ref long streamPosition)
        {
            CheckDisposed();
            if (buffer == null)
            {
                ThrowHelper.ThrowArgumentNullException("buffer");
                return -1;
            }

            if (offset < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("offset");
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("count");
            }

            if (offset + count > buffer.Length)
            {
                ThrowHelper.ThrowArgumentException("buffer length must be at least offset + count");
            }

            var amountRead = InternalRead(buffer, offset, count, streamPosition);
            streamPosition += amountRead;
            return amountRead;
        }

        /// <summary>
        /// Writes the buffer to the stream
        /// </summary>
        /// <param name="buffer">Source buffer</param>
        /// <param name="offset">Start position</param>
        /// <param name="count">Number of bytes to write</param>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative</exception>
        /// <exception cref="ArgumentException">buffer.Length - offset is not less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            SafeWrite(buffer, offset, count);
        }

        /// <summary>
        /// Writes the buffer to the stream
        /// </summary>
        /// <param name="buffer">Source buffer</param>
        /// <param name="offset">Start position</param>
        /// <param name="count">Number of bytes to write</param>
        /// <exception cref="ArgumentNullException">buffer is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative</exception>
        /// <exception cref="ArgumentException">buffer.Length - offset is not less than count</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SafeWrite(byte[] buffer, int offset, int count)
        {
            CheckDisposed();
            if (buffer == null)
            {
                ThrowHelper.ThrowArgumentNullException("buffer");
                return;
            }

            if (offset < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("offset");
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("count");
            }

            if (count + offset > buffer.Length)
            {
                ThrowHelper.ThrowArgumentException("count must be greater than buffer.Length - offset");
            }

            var blockSize = _memoryManager.BlockSize;
            var end = _position + count;
            // Check for overflow
            if (end > MaxStreamLength)
            {
                ThrowHelper.ThrowIOException("Maximum capacity exceeded");
            }

            var requiredBuffers = (end + blockSize - 1) / blockSize;

            if (requiredBuffers * blockSize > MaxStreamLength)
            {
                ThrowHelper.ThrowIOException("Maximum capacity exceeded");
            }

            EnsureCapacity((int)end);

            if (_largeBuffer.IsEmpty)
            {
                var bytesRemaining = count;
                var bytesWritten = 0;
                var blockAndOffset = GetBlockAndRelativeOffset(_position);

                while (bytesRemaining > 0)
                {
                    var currentBlock = _blocks[blockAndOffset.Block];
                    var remainingInBlock = blockSize - blockAndOffset.Offset;
                    var amountToWriteInBlock = Math.Min(remainingInBlock, bytesRemaining);
                    Unsafe.CopyBlockUnaligned(ref currentBlock[blockAndOffset.Offset], ref buffer[offset + bytesWritten], (uint)amountToWriteInBlock);

                    bytesRemaining -= amountToWriteInBlock;
                    bytesWritten += amountToWriteInBlock;

                    ++blockAndOffset.Block;
                    blockAndOffset.Offset = 0;
                }
            }
            else
            {
                Unsafe.CopyBlockUnaligned(ref _largeBuffer.Span[checked((int)_position)], ref buffer[offset], (uint)count);
            }
            _position = (int)end;
            _length = Math.Max(_position, _length);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SafeWrite(Memory<byte> buffer)
        {
            CheckDisposed();
            if (buffer.IsEmpty)
            {
                return;
            }

            var blockSize = _memoryManager.BlockSize;
            var end = _position + buffer.Length;
            // Check for overflow
            if (end > MaxStreamLength)
            {
                ThrowHelper.ThrowIOException("Maximum capacity exceeded");
            }

            var requiredBuffers = (end + blockSize - 1) / blockSize;

            if (requiredBuffers * blockSize > MaxStreamLength)
            {
                ThrowHelper.ThrowIOException("Maximum capacity exceeded");
            }

            EnsureCapacity((int)end);

            if (_largeBuffer.IsEmpty)
            {
                var bytesRemaining = buffer.Length;
                var bytesWritten = 0;
                var blockAndOffset = GetBlockAndRelativeOffset(_position);

                while (bytesRemaining > 0)
                {
                    var currentBlock = _blocks[blockAndOffset.Block];
                    var remainingInBlock = blockSize - blockAndOffset.Offset;
                    var amountToWriteInBlock = Math.Min(remainingInBlock, bytesRemaining);
                    Unsafe.CopyBlockUnaligned(ref currentBlock[blockAndOffset.Offset], ref buffer.Span[bytesWritten], (uint)amountToWriteInBlock);

                    bytesRemaining -= amountToWriteInBlock;
                    bytesWritten += amountToWriteInBlock;

                    ++blockAndOffset.Block;
                    blockAndOffset.Offset = 0;
                }
            }
            else
            {
                Unsafe.CopyBlockUnaligned(ref _largeBuffer.Span[checked((int)_position)], ref buffer.Span[0], (uint)buffer.Length);
            }
            _position = (int)end;
            _length = Math.Max(_position, _length);
        }

        /// <summary>
        /// Returns a useful string for debugging. This should not normally be called in actual production code.
        /// </summary>
        public override string ToString()
        {
            return string.Format("Id = {0}, Tag = {1}, Length = {2:N0} bytes", Id, Tag, Length);
        }

        /// <summary>
        /// Writes a single byte to the current position in the stream.
        /// </summary>
        /// <param name="value">byte value to write</param>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override void WriteByte(byte value)
        {
            SafeWriteByte(value);
        }

        /// <summary>
        /// Writes a single byte to the current position in the stream.
        /// </summary>
        /// <param name="value">byte value to write</param>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SafeWriteByte(byte value)
        {
            CheckDisposed();
            var end = _position + 1;
            // Check for overflow
            if (end > MaxStreamLength)
            {
                ThrowHelper.ThrowIOException("Maximum capacity exceeded");
            }

            EnsureCapacity((int)end);

            if (_largeBuffer.IsEmpty)
            {
                var blockAndOffset = GetBlockAndRelativeOffset(_position);

                var currentBlock = _blocks[blockAndOffset.Block];

                currentBlock[blockAndOffset.Offset] = value;
            }
            else
            {
                _largeBuffer.Span[checked((int)_position)] = value;
            }
            _position = (int)end;
            _length = Math.Max(_position, _length);
        }

        /// <summary>
        /// Reads a single byte from the current position in the stream.
        /// </summary>
        /// <returns>The byte at the current position, or -1 if the position is at the end of the stream.</returns>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override int ReadByte()
        {
            return SafeReadByte(ref _position);
        }

        /// <summary>
        /// Reads a single byte from the specified position in the stream.
        /// </summary>
        /// <param name="streamPosition">The position in the stream to read from</param>
        /// <returns>The byte at the current position, or -1 if the position is at the end of the stream.</returns>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SafeReadByte(ref long streamPosition)
        {
            CheckDisposed();
            if (streamPosition == _length)
            {
                return -1;
            }
            byte value;
            if (_largeBuffer.IsEmpty)
            {
                var blockAndOffset = GetBlockAndRelativeOffset(streamPosition);
                value = _blocks[blockAndOffset.Block][blockAndOffset.Offset];
            }
            else
            {
                value = _largeBuffer.Span[checked((int)streamPosition)];
            }
            streamPosition++;
            return value;
        }

        /// <summary>
        /// Sets the length of the stream
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">value is negative or larger than MaxStreamLength</exception>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        public override void SetLength(long value)
        {
            CheckDisposed();
            if (value < 0 || value > MaxStreamLength)
            {
                throw new ArgumentOutOfRangeException("value", "value must be non-negative and at most " + MaxStreamLength);
            }

            EnsureCapacity((int)value);

            _length = (int)value;
            if (_position > value)
            {
                _position = (int)value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetLengthInternal(long value)
        {
            if (value < 0 || value > MaxStreamLength)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("value must be non-negative and at most " + MaxStreamLength);
            }

            EnsureCapacity((int)value);

            _length = (int)value;
            if (_position > value)
            {
                _position = (int)value;
            }
        }

        /// <summary>
        /// Sets the position to the offset from the seek location
        /// </summary>
        /// <param name="offset">How many bytes to move</param>
        /// <param name="loc">From where</param>
        /// <returns>The new position</returns>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset is larger than MaxStreamLength</exception>
        /// <exception cref="ArgumentException">Invalid seek origin</exception>
        /// <exception cref="IOException">Attempt to set negative position</exception>
        public override long Seek(long offset, SeekOrigin loc)
        {
            CheckDisposed();
            if (offset > MaxStreamLength)
            {
                throw new ArgumentOutOfRangeException("offset", "offset cannot be larger than " + MaxStreamLength);
            }

            long newPosition;
            switch (loc)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;

                case SeekOrigin.Current:
                    newPosition = offset + _position;
                    break;

                case SeekOrigin.End:
                    newPosition = offset + _length;
                    break;

                default:
                    throw new ArgumentException("Invalid seek origin", "loc");
            }
            if (newPosition < 0)
            {
                throw new IOException("Seek before beginning");
            }
            _position = newPosition;
            return _position;
        }

        /// <summary>
        /// Synchronously writes this stream's bytes to the parameter stream.
        /// </summary>
        /// <param name="stream">Destination stream</param>
        /// <remarks>Important: This does a synchronous write, which may not be desired in some situations</remarks>
        public override void WriteTo(Stream stream)
        {
            CheckDisposed();
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (_largeBuffer.IsEmpty)
            {
                var currentBlock = 0;
                var bytesRemaining = _length;

                while (bytesRemaining > 0)
                {
                    var amountToCopy = Math.Min(_blocks[currentBlock].Length, bytesRemaining);
                    stream.Write(_blocks[currentBlock], 0, checked((int)amountToCopy));

                    bytesRemaining -= amountToCopy;

                    ++currentBlock;
                }
            }
            else
            {
                if (_length > int.MaxValue)
                {
                    ThrowHelper.ThrowNotImplementedException("Large arrays are not implemented yet in RMS");
                }
#if NETCOREAPP2_1
                stream.Write(_largeBuffer.Span.Slice(0, checked((int)_length)));
#else
                if (_largeBuffer.TryGetArray(out var segment))
                {
                    stream.Write(segment.Array, segment.Offset, checked((int)_length));
                }
                else
                {
                    var array = _largeBuffer.Memory.ToArray();
                    stream.Write(array, 0, checked((int)_length));
                }
               
#endif
            }
        }

#endregion MemoryStream overrides

        public struct ChunksEnumerable : IEnumerable<Memory<byte>>
        {
            private RecyclableMemoryStream _rms;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ChunksEnumerable(RecyclableMemoryStream rms)
            {
                _rms = rms;
            }

            [Obsolete("Hide API")]
            public Memory<byte> LargeBuffer
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _rms._largeBuffer.Memory; }
            }

            [Obsolete("Hide API")]
            public List<byte[]> RawChunks
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return _rms._blocks; }
            }

            public struct ChunksEnumerator : IEnumerator<Memory<byte>>
            {
                private int _idx;
                private RecyclableMemoryStream _rms;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public ChunksEnumerator(RecyclableMemoryStream rms)
                {
                    if (!rms._largeBuffer.IsEmpty)
                    {
                        _idx = int.MinValue;
                    }
                    else
                    {
                        _idx = -1;
                    }
                    _rms = rms;
                    Current = default;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    if (!_rms._largeBuffer.IsEmpty)
                    {
                        if (_idx == Int32.MinValue)
                        {
                            Current = _rms._largeBuffer.Memory.Slice(0, checked((int)_rms._length)); // AS doesn't support long, will throw
                            _idx = Int32.MaxValue;
                            return true;
                        }
                        return false;
                    }

                    _idx++;
                    if (_idx < _rms._blocks.Count)
                    {
                        var remainingLength = _rms._length - _idx * _rms._memoryManager.BlockSize;
                        if (remainingLength <= 0)
                        {
                            return false;
                        }
                        var len = remainingLength < _rms._memoryManager.BlockSize
                            // last chunk
                            ? remainingLength
                            // full chunk
                            : _rms._memoryManager.BlockSize;
                        Current = new ArraySegment<byte>(_rms._blocks[_idx], 0, checked((int)len));
                        return true;
                    }
                    return false;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Reset()
                {
                    if (!_rms._largeBuffer.IsEmpty)
                    {
                        _idx = int.MinValue;
                    }
                    else
                    {
                        _idx = -1;
                    }
                }

                public Memory<byte> Current { [MethodImpl(MethodImplOptions.AggressiveInlining)]get; [MethodImpl(MethodImplOptions.AggressiveInlining)]private set; }

                object IEnumerator.Current => Current;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Dispose()
                {
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ChunksEnumerator GetEnumerator()
            {
                return new ChunksEnumerator(_rms);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator<Memory<byte>> IEnumerable<Memory<byte>>.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Obsolete("Hide API")]
        public bool IsSingleChunk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (!_largeBuffer.IsEmpty || _blocks.Count == 1) && _length > 0;
            }
        }

        [Obsolete("Hide API")]
        public Memory<byte> SingleChunk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return IsSingleChunk ? (_largeBuffer.IsEmpty ? _blocks[0] : _largeBuffer.Memory) : Memory<byte>.Empty; }
        }

        /// <summary>
        /// Iterate over all internal chunks as ArraySegments without copying data
        /// </summary>
        public ChunksEnumerable Chunks
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return new ChunksEnumerable(this);
            }
        }

        public override bool TryGetBuffer(out ArraySegment<byte> buffer)
        {
            buffer = default;
            return false;
        }

#region Helper Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckDisposed()
        {
            if (IntPtr.Size == 8 ? Volatile.Read(ref _refCount) == 0 : Interlocked.Read(ref _refCount) == 0)
            {
#if DEBUG
                ThrowHelper.ThrowObjectDisposedException($"The stream with Id {_id} and Tag {_tag} is disposed.");
#else
                ThrowHelper.ThrowObjectDisposedException($"The stream is disposed.");
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long InternalRead(byte[] buffer, long offset, long count, long fromPosition)
        {
            if (_length - fromPosition <= 0)
            {
                return 0;
            }
            if (_largeBuffer.IsEmpty)
            {
                var blockAndOffset = GetBlockAndRelativeOffset(fromPosition);
                var bytesWritten = 0L;
                var bytesRemaining = Math.Min(count, _length - fromPosition);

                while (bytesRemaining > 0)
                {
                    var amountToCopy = Math.Min(_blocks[blockAndOffset.Block].Length - blockAndOffset.Offset, bytesRemaining);

                    System.Runtime.CompilerServices.Unsafe
                        .CopyBlockUnaligned(ref buffer[bytesWritten + offset], ref _blocks[blockAndOffset.Block][blockAndOffset.Offset], (uint)amountToCopy);

                    bytesWritten += amountToCopy;
                    bytesRemaining -= amountToCopy;

                    ++blockAndOffset.Block;
                    blockAndOffset.Offset = 0;
                }
                return bytesWritten;
            }
            else
            {
                var amountToCopy = Math.Min(count, _length - fromPosition);
                Unsafe.CopyBlockUnaligned(ref buffer[offset], ref _largeBuffer.Span[checked((int)fromPosition)], (uint)amountToCopy);
                return amountToCopy;
            }
        }

        private struct BlockAndOffset
        {
            public int Block;
            public int Offset;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BlockAndOffset(int block, int offset)
            {
                Block = block;
                Offset = offset;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BlockAndOffset GetBlockAndRelativeOffset(long offset)
        {
            var blockSize = _memoryManager.BlockSize;
            var block = offset / blockSize;
            return new BlockAndOffset(checked((int)block), checked((int)(offset - block * blockSize)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int newCapacity)
        {
            if (newCapacity > _memoryManager.MaximumStreamCapacity && _memoryManager.MaximumStreamCapacity > 0)
            {
#if DEBUG
                Events.Write.MemoryStreamOverCapacity(newCapacity, _memoryManager.MaximumStreamCapacity, _tag, AllocationStack);
#else
                Events.Write.MemoryStreamOverCapacity(newCapacity, _memoryManager.MaximumStreamCapacity, null, AllocationStack);
#endif
                ThrowHelper.ThrowInvalidOperationException("Requested capacity is too large: " + newCapacity + ". Limit is " + _memoryManager.MaximumStreamCapacity);
            }

            if (!_largeBuffer.IsEmpty)
            {
                if (newCapacity > _largeBuffer.Length)
                {
#if DEBUG
                    var newBuffer = _memoryManager.GetLargeBuffer(newCapacity, _tag);
#else
                    var newBuffer = _memoryManager.GetLargeBuffer(newCapacity, null);
#endif
                    InternalRead(newBuffer, 0, _length, 0);
                    _largeBuffer.Dispose();
                    _largeBuffer = new RetainedMemory<byte>(newBuffer);
                }
            }
            else
            {
                while (CapacityInternal < newCapacity)
                {
                    _blocks.Add((_memoryManager.GetBlock()));
                }
            }
        }

#endregion Helper Methods
    }
}