// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    /// <summary>
    /// Unsafe fast buffer writer.
    /// </summary>
    public unsafe class BufferWriter : IBufferWriter<byte>, IDisposable
    {
        private static readonly ObjectPool<BufferWriter> ObjectPool = new ObjectPool<BufferWriter>(() => new BufferWriter(), Environment.ProcessorCount * 4);

        private BufferWriter()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferWriter Create(int capacityHint = 0)
        {
            var buffer = ObjectPool.Allocate();
            buffer._offset = 0;
            if (capacityHint > buffer.FreeCapacity)
            {
                buffer.EnsureCapacity(capacityHint);
            }
            return buffer;
        }

        internal const int MinLen = 16 * 1024;
        internal const int MaxLen = 1024 * 1024 * 1024;

        private RetainedMemory<byte> _buffer;
        private int _offset;

        public int WrittenLength => _offset;

        public bool IsEmpty => _offset == 0;

        public bool IsDisposed => _offset == -1;

        public ReadOnlySpan<byte> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return new Span<byte>(_buffer.Pointer, _offset);
            }
        }

        public ReadOnlyMemory<byte> WrittenMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return _buffer.Memory.Slice(0, _offset);
            }
        }

        internal DirectBuffer WrittenBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(_offset, (byte*)_buffer.Pointer);
        }

        public Span<byte> FreeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return new Span<byte>(FirstFreePointer, FreeCapacity);
            }
        }

        public Memory<byte> FreeMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return _buffer.Memory.Slice(_offset, FreeCapacity);
            }
        }

        internal DirectBuffer FreeBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(FreeCapacity, FirstFreePointer);
        }

        public int FreeCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                return _buffer.Length - checked((int)(uint)_offset);
            }
        }

        internal ref byte FirstFreeByte
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *((byte*)_buffer.Pointer + _offset);
        }

        internal byte* FirstFreePointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((byte*)_buffer.Pointer + _offset);
        }

        /// <summary>
        /// Ensures capacity and writes an unmanaged structure <paramref name="value"/> at current offset.
        /// Type <typeparamref name="T"/> is not checked in release mode. This is equivalent of calling
        /// <see cref="Unsafe.WriteUnaligned{T}(ref byte,T)"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write<T>(in T value)
        {
            Debug.Assert(TypeHelper<T>.IsFixedSize, "BufferWrite.Write<T> works only when TypeHelper<T>.IsFixedSize.");
            var appendLength = Unsafe.SizeOf<T>();
            EnsureCapacity(appendLength);
            Unsafe.WriteUnaligned(ref *((byte*)_buffer.Pointer + _offset), value);
            _offset += appendLength;
            return appendLength;
        }

        /// <summary>
        /// Write at custom offset. Could overwrite data in already written segment.
        /// Usually this is the intent when using this method but be very careful.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int DangerousWriteAtOffset<T>(T value, int offset)
        {
            Debug.Assert(TypeHelper<T>.IsFixedSize, "BufferWrite.Write<T> works only when TypeHelper<T>.IsFixedSize.");
            var appendLength = Math.Max(0, offset + Unsafe.SizeOf<T>() - _offset);
            EnsureCapacity(appendLength);
            Unsafe.WriteUnaligned(ref *((byte*)_buffer.Pointer + offset), value);
            _offset += appendLength;
            return appendLength;
        }

        /// <summary>
        /// Ensures capacity for and writes the entire content of <paramref name="buffer"/>.
        /// Returns the number of bytes written, which must equal to <paramref name="buffer"/> length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(DirectBuffer buffer)
        {
            var appendLength = buffer.Length;
            EnsureCapacity(appendLength);

            buffer.Span.CopyTo(_buffer.Span.Slice(_offset));
            _offset += appendLength;
            return appendLength;
        }

        /// <summary>
        /// Ensures capacity for and writes the entire content of <paramref name="span"/>.
        /// Returns the number of bytes written, which must equal to <paramref name="span"/> length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteSpan(ReadOnlySpan<byte> span)
        {
            var appendLength = span.Length;
            EnsureCapacity(appendLength);

            span.CopyTo(_buffer.Span.Slice(_offset));
            _offset += appendLength;
            return appendLength;
        }

        /// <summary>
        /// Ensures capacity and writes <paramref name="length"/> number of bytes starting from <paramref name="source"/> byte reference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Write(ref byte source, uint length)
        {
            var appendLength = (int)length;
            EnsureCapacity(appendLength);

            Unsafe.CopyBlockUnaligned(ref *((byte*)_buffer.Pointer + _offset), ref source, length);
            _offset += appendLength;
            return appendLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int WriteByte(byte value)
        {
            const int appendLength = 1;
            EnsureCapacity(appendLength);

            Unsafe.Write((byte*)_buffer.Pointer + _offset, value);
            _offset += appendLength;
            return appendLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write7BitEncodedInt(ulong value)
        {
            var len = 0;
            while (value >= 0x80)
            {
                len += WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            len += WriteByte((byte)value);
            return len;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            CheckDisposed();
            _offset += count;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.Memory.Slice(_offset);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.Span.Slice(_offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int appendLength)
        {
            // we will get a huge number when _offset == -1 and resize path checks for disposed
            var newLength = unchecked((uint)_offset + (uint)appendLength);
            var current = (uint)_buffer.Length;
            if (newLength > current)
            {
                EnsureCapacityResize(appendLength);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private void EnsureCapacityResize(int appendLength)
        {
            CheckDisposed();

            var newLength = BitUtil.FindNextPositivePowerOfTwo(_offset + appendLength);
            if (newLength < MinLen)
            {
                newLength = MinLen;
            }

            if (newLength > MaxLen)
            {
                if (_offset + appendLength > MaxLen)
                {
                    ThrowHelper.ThrowNotSupportedException("_offset + appendLength > MaxLen");
                }

                newLength = MaxLen;
            }

            // Instead of providing options here we have ways
            // to customize behavior of the temp pool.
            var newBuffer = BufferPool.RetainTemp(newLength);
            Debug.Assert(newBuffer.IsPinned);
            if (!_buffer.IsEmpty && _offset > 0)
            {
                _buffer.Span.Slice(0, _offset).CopyTo(newBuffer.Span);
            }

            var oldBuffer = _buffer;
            _buffer = newBuffer;
            oldBuffer.Dispose();
        }

        /// <summary>
        /// Returns <see cref="WrittenMemory"/> as <see cref="RetainedMemory{T}"/> and disposes this <see cref="BufferWriter"/>.
        /// The returned memory must be disposed after processing.
        /// The memory is not owned (<see cref="RetainedMemory{T}.ReferenceCount"/> is zero), therefore if multiple
        /// consumers will process the memory it must be retained by the caller using  <see cref="RetainedMemory{T}.Retain()" />
        /// and by each additional consumer by calling <see cref="RetainedMemory{T}.Clone()"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RetainedMemory<byte> DetachMemory()
        {
            CheckDisposed();
            _offset = -1;
            var buffer = _buffer;
            _buffer = default;
            ObjectPool.Free(this);
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Dispose(bool disposing)
        {
            _offset = -1;
            if (disposing)
            {
                if (_buffer.Length > MinLen)
                {
                    _buffer.Dispose();
                    _buffer = default;
                }
                ObjectPool.Free(this);
            }
            else
            {
                _buffer.Dispose();
                _buffer = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // at any point the number of active writers should be ~CPU count
        ~BufferWriter()
        {
            Dispose(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckDisposed()
        {
            if (AdditionalCorrectnessChecks.Enabled && _offset < 0)
            {
                BuffersThrowHelper.ThrowDisposed<BufferWriter>();
            }
        }
    }
}
