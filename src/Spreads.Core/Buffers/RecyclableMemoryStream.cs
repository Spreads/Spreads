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
using Spreads.Utils;
using System;
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

        private static readonly byte[] EmptyArray = new byte[0];

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
        private byte[] _largeBuffer;

        /// <summary>
        /// This list is used to store buffers once they're replaced by something larger.
        /// This is for the cases where you have users of this class that may hold onto the buffers longer
        /// than they should and you want to prevent race conditions which could corrupt the data.
        /// </summary>
        private List<byte[]> _dirtyBuffers;

        private static long _lastId;
        private long _id;

        /// <summary>
        /// Unique identifier for this stream across it's entire lifetime
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal long Id { get { CheckDisposed(); return _id; } }

        private string _tag;

        /// <summary>
        /// A temporary identifier for the current usage of this stream.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Object has been disposed</exception>
        internal string Tag { get { CheckDisposed(); return _tag; } }

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

        // private bool _disposed;

        // long to allow Interlocked.Read (for .NET Standard 1.4 compat)
        private long _disposedState;

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

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        /// <param name="requestedSize">The initial requested size to prevent future allocations</param>
        /// <param name="initialLargeBuffer">An initial buffer to use. This buffer will be owned by the stream and returned to the memory manager upon Dispose.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RecyclableMemoryStream Create(RecyclableMemoryStreamManager memoryManager, string tag, int requestedSize,
            byte[] initialLargeBuffer)
        {
            var rms = Pool.Allocate();
            rms._disposedState = 0;
            rms._memoryManager = memoryManager;
            rms._id = Interlocked.Increment(ref _lastId);
            rms._tag = tag;

            if (requestedSize < memoryManager.BlockSize)
            {
                requestedSize = memoryManager.BlockSize;
            }

            if (initialLargeBuffer == null)
            {
                rms.EnsureCapacity(requestedSize);
            }
            else
            {
                rms._largeBuffer = initialLargeBuffer;
            }

            if (rms._memoryManager.GenerateCallStacks)
            {
                rms.AllocationStack = Environment.StackTrace;
            }

            Events.Write.MemoryStreamCreated(rms._id, rms._tag, requestedSize);
            rms._memoryManager.ReportStreamCreated();

            return rms;
        }

        private RecyclableMemoryStream() : base(EmptyArray)
        {
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object.
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        public static RecyclableMemoryStream Create(RecyclableMemoryStreamManager memoryManager)
        {
            return Create(memoryManager, null, 0, null);
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        public static RecyclableMemoryStream Create(RecyclableMemoryStreamManager memoryManager, string tag)
        {
            return Create(memoryManager, tag, 0, null);
        }

        /// <summary>
        /// Allocate a new RecyclableMemoryStream object
        /// </summary>
        /// <param name="memoryManager">The memory manager</param>
        /// <param name="tag">A string identifying this stream for logging and debugging purposes</param>
        /// <param name="requestedSize">The initial requested size to prevent future allocations</param>
        public static RecyclableMemoryStream Create(RecyclableMemoryStreamManager memoryManager, string tag, int requestedSize)
        {
            return Create(memoryManager, tag, requestedSize, null);
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

            if (Interlocked.CompareExchange(ref _disposedState, 1, 0) != 0)
            {
                string doubleDisposeStack = null;
                if (_memoryManager.GenerateCallStacks)
                {
                    doubleDisposeStack = Environment.StackTrace;
                }

                Events.Write.MemoryStreamDoubleDispose(_id, _tag, AllocationStack, DisposeStack,
                    doubleDisposeStack);
                return;
            }

            Events.Write.MemoryStreamDisposed(_id, _tag);

            if (_memoryManager.GenerateCallStacks)
            {
                DisposeStack = Environment.StackTrace;
            }

            if (disposing)
            {
                _memoryManager.ReportStreamDisposed();

                // regardless of Free result below we do not need finalization, we have done cleaning
                GC.SuppressFinalize(this);
            }
            else
            {
                // We're being finalized.
                Events.Write.MemoryStreamFinalized(_id, _tag, AllocationStack);
                if (AppDomain.CurrentDomain.IsFinalizingForUnload()) {
                    // If we're being finalized because of a shutdown, don't go any further.
                    // We have no idea what's already been cleaned up. Triggering events may cause
                    // a crash.
                    base.Dispose(false);
                    return;
                }
                _memoryManager.ReportStreamFinalized();
            }

            _memoryManager.ReportStreamLength(_length);

            if (_largeBuffer != null)
            {
                _memoryManager.ReturnLargeBuffer(_largeBuffer, _tag);
                _largeBuffer = null;
            }

            if (_dirtyBuffers != null)
            {
                foreach (var buffer in _dirtyBuffers)
                {
                    _memoryManager.ReturnLargeBuffer(buffer, _tag);
                }
                _dirtyBuffers.Clear();
                _dirtyBuffers = null;
            }

            _memoryManager.ReturnBlocks(_blocks, _tag);
            _blocks.Clear();

            _id = 0;

            _length = 0;
            _position = 0;
            _tag = null;

            _memoryManager = null;

            base.Dispose(disposing);

            // last operation, prevent race condition (had it with _memoryManager = null when buffer was reused before Dispose finished)
            Pool.Free(this);

        }

        /// <summary>
        /// Equivalent to Dispose
        /// </summary>
        public override void Close() {
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
                return CapacityPrivate;
            }
            set
            {
                CheckDisposed();
                EnsureCapacity(value);
            }
        }

        private int CapacityPrivate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_largeBuffer != null)
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
            get
            {
                CheckDisposed();
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
            get
            {
                CheckDisposed();
                return _position;
            }
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

        /// <summary>
        /// Whether the stream can currently read
        /// </summary>
        public override bool CanRead => Interlocked.Read(ref _disposedState) == 0;

        /// <summary>
        /// Whether the stream can currently seek
        /// </summary>
        public override bool CanSeek => Interlocked.Read(ref _disposedState) == 0;

        /// <summary>
        /// Always false
        /// </summary>
        public override bool CanTimeout => false;

        /// <summary>
        /// Whether the stream can currently write
        /// </summary>
        public override bool CanWrite => Interlocked.Read(ref _disposedState) == 0;

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
            CheckDisposed();

            if (_largeBuffer != null)
            {
                return _largeBuffer;
            }

            if (_blocks.Count == 1)
            {
                return _blocks[0];
            }

            // Memory needs to reflect the capacity, not the length, because
            // it's possible that people will manipulate the buffer directly
            // and set the length afterward. Capacity sets the expectation
            // for the size of the buffer.
            var newBuffer = _memoryManager.GetLargeBuffer(Capacity, _tag);

            // InternalRead will check for existence of largeBuffer, so make sure we
            // don't set it until after we've copied the data.
            InternalRead(newBuffer, 0, _length, 0);
            _largeBuffer = newBuffer;

            if (_blocks.Count > 0 && _memoryManager.AggressiveBufferReturn)
            {
                _memoryManager.ReturnBlocks(_blocks, _tag);
                _blocks.Clear();
            }

            return _largeBuffer;
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
            Events.Write.MemoryStreamToArray(_id, _tag, stack, 0);
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

            if (_largeBuffer == null)
            {
                var bytesRemaining = count;
                var bytesWritten = 0;
                var blockAndOffset = GetBlockAndRelativeOffset(_position);

                while (bytesRemaining > 0)
                {
                    var currentBlock = _blocks[blockAndOffset.Block];
                    var remainingInBlock = blockSize - blockAndOffset.Offset;
                    var amountToWriteInBlock = Math.Min(remainingInBlock, bytesRemaining);

                    ByteUtil.VectorizedCopy(buffer, offset + bytesWritten, currentBlock, blockAndOffset.Offset, amountToWriteInBlock);

                    bytesRemaining -= amountToWriteInBlock;
                    bytesWritten += amountToWriteInBlock;

                    ++blockAndOffset.Block;
                    blockAndOffset.Offset = 0;
                }
            }
            else
            {
                ByteUtil.VectorizedCopy(buffer, offset, _largeBuffer, _position, count);
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

            if (_largeBuffer == null)
            {
                var blockAndOffset = GetBlockAndRelativeOffset(_position);

                var currentBlock = _blocks[blockAndOffset.Block];

                currentBlock[blockAndOffset.Offset] = value;
            }
            else
            {
                _largeBuffer[_position] = value;
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
            if (_largeBuffer == null)
            {
                var blockAndOffset = GetBlockAndRelativeOffset(streamPosition);
                value = _blocks[blockAndOffset.Block][blockAndOffset.Offset];
            }
            else
            {
                value = _largeBuffer[streamPosition];
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

            if (_largeBuffer == null)
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
                stream.Write(_largeBuffer, 0, checked((int)_length)); // TODO we could write in chunks
            }
        }

        #endregion MemoryStream overrides

        /// <summary>
        /// Iterate over all internal chunks as ArraySegments without copying data
        /// </summary>
        public IEnumerable<ArraySegment<byte>> Chunks
        {
            get
            {
                CheckDisposed();
                if (_largeBuffer != null)
                {
                    yield return new ArraySegment<byte>(_largeBuffer, 0, checked((int)_length)); // AS doesn't support long, will throw
                }
                else
                {
                    for (var i = 0; i < _blocks.Count; i++)
                    {
                        var len = (i == _blocks.Count - 1)
                            // last chunk
                            ? _length - (_blocks.Count - 1) * _memoryManager.BlockSize
                            // full chunk
                            : _memoryManager.BlockSize;
                        yield return new ArraySegment<byte>(_blocks[i], 0, checked((int)len)); // Chuncks should never be > Int32.Max
                    }
                }
            }
        }

        #region Helper Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckDisposed()
        {
            if (Interlocked.Read(ref _disposedState) != 0)
            {
                ThrowHelper.ThrowObjectDisposedException(string.Format("The stream with Id {0} and Tag {1} is disposed.", _id, _tag));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long InternalRead(byte[] buffer, long offset, long count, long fromPosition)
        {
            if (_length - fromPosition <= 0)
            {
                return 0;
            }
            if (_largeBuffer == null)
            {
                var blockAndOffset = GetBlockAndRelativeOffset(fromPosition);
                var bytesWritten = 0L;
                var bytesRemaining = Math.Min(count, _length - fromPosition);

                while (bytesRemaining > 0)
                {
                    var amountToCopy = Math.Min(_blocks[blockAndOffset.Block].Length - blockAndOffset.Offset, bytesRemaining);
                    ByteUtil.VectorizedCopy(_blocks[blockAndOffset.Block], blockAndOffset.Offset, buffer, bytesWritten + offset, amountToCopy);

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
                ByteUtil.VectorizedCopy(_largeBuffer, fromPosition, buffer, offset, amountToCopy);
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
                Events.Write.MemoryStreamOverCapacity(newCapacity, _memoryManager.MaximumStreamCapacity, _tag, AllocationStack);
                ThrowHelper.ThrowInvalidOperationException("Requested capacity is too large: " + newCapacity + ". Limit is " + _memoryManager.MaximumStreamCapacity);
            }

            if (_largeBuffer != null)
            {
                if (newCapacity > _largeBuffer.Length)
                {
                    var newBuffer = _memoryManager.GetLargeBuffer(newCapacity, _tag);
                    InternalRead(newBuffer, 0, _length, 0);
                    ReleaseLargeBuffer();
                    _largeBuffer = newBuffer;
                }
            }
            else
            {
                while (CapacityPrivate < newCapacity)
                {
                    _blocks.Add((_memoryManager.GetBlock()));
                }
            }
        }

        /// <summary>
        /// Release the large buffer (either stores it for eventual release or returns it immediately).
        /// </summary>
        private void ReleaseLargeBuffer()
        {
            if (_memoryManager.AggressiveBufferReturn)
            {
                _memoryManager.ReturnLargeBuffer(_largeBuffer, _tag);
            }
            else
            {
                if (_dirtyBuffers == null)
                {
                    // We most likely will only ever need space for one
                    _dirtyBuffers = new List<byte[]>(1);
                }
                _dirtyBuffers.Add(_largeBuffer);
            }

            _largeBuffer = null;
        }

        #endregion Helper Methods
    }
}