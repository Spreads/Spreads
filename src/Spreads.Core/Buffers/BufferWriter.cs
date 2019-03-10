// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Collections.Concurrent;
using Spreads.Serialization;
using Spreads.Utils;

namespace Spreads.Buffers
{
    /// <summary>
    /// Unsafe buffer reader.
    /// </summary>
    public unsafe class BufferWriter : IBufferWriter<byte>, IDisposable
    {
        private static readonly ObjectPool<BufferWriter> ObjectPool = new ObjectPool<BufferWriter>(() => new BufferWriter(), Environment.ProcessorCount * 16);

        private BufferWriter()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BufferWriter Create()
        {
            var buffer = ObjectPool.Allocate();
            buffer._offset = 0;
            return buffer;
        }

        internal const int MinLen = 2048;
        internal const int MaxLen = 1024 * 1024 * 1024;

        private RetainedMemory<byte> _buffer;
        private int _offset;

        public int Offset => _offset;
        public bool IsEmpty => _offset == 0;

        public ReadOnlySpan<byte> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<byte>(_buffer.Pointer, _offset);
        }

        internal DirectBuffer WrittenBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(_offset, (byte*)_buffer.Pointer);
        }

        internal DirectBuffer AvailableBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(AvailableLength, FirstFreePointer);
        }

        public int AvailableLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length - _offset;
        }

        public ref byte FirstFreeByte
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *((byte*)_buffer.Pointer + _offset);
        }

        public byte* FirstFreePointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((byte*)_buffer.Pointer + _offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write<T>(T value)
        {
            Debug.Assert(TypeHelper<T>.IsFixedSize, "BufferWrite.Write<T> works only when TypeHelper<T>.IsFixedSize.");

            var appendLength = Unsafe.SizeOf<T>();
            EnsureCapacity(appendLength);
            Unsafe.WriteUnaligned(ref *((byte*)_buffer.Pointer + _offset), value);
            _offset += appendLength;
            return appendLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(DirectBuffer value)
        {
            var appendLength = value.Length;
            EnsureCapacity(appendLength);

            value.Span.CopyTo(_buffer.Span.Slice(_offset));
            _offset += appendLength;
            return appendLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(ReadOnlySpan<byte> value)
        {
            var appendLength = value.Length;
            EnsureCapacity(appendLength);

            value.CopyTo(_buffer.Span.Slice(_offset));
            _offset += appendLength;
            return appendLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(ref byte source, uint length)
        {
            var appendLength = (int)length;
            EnsureCapacity(appendLength);

            Unsafe.CopyBlockUnaligned(ref *((byte*)_buffer.Pointer + _offset), ref source, length);
            _offset += appendLength;
            return appendLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            _offset += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.Memory.Slice(_offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.Span.Slice(_offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int appendLength)
        {
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
            if (_offset == -1)
            {
                BuffersThrowHelper.ThrowDisposed<BufferWriter>();
            }
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

            var newBuffer = BufferPool.RetainTemp(newLength);
            Debug.Assert(newBuffer.IsPinned);
            if (!_buffer.IsEmpty)
            {
                _buffer.Span.CopyTo(newBuffer.Span);
            }

            var oldBuffer = _buffer;
            _buffer = newBuffer;
            oldBuffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Dispose(bool disposing)
        {
            _offset = -1;
            if (disposing)
            {
                ObjectPool.Free(this);
            }
            else
            {
                _buffer.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Dispose(true);
        }

        // at any point the number of active writers should be ~CPU count
        ~BufferWriter()
        {
            Dispose(false);
        }
    }
}