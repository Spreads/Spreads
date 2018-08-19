// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Buffers
{
    /// <summary>
    /// Provides unsafe read/write opertaions on a memory pointer.
    /// </summary>
    [DebuggerDisplay("Length={" + nameof(Length) + ("}"))]
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct DirectBuffer
    {
        public static DirectBuffer Invalid = new DirectBuffer(-1, (byte*)IntPtr.Zero);

        private readonly long _length;
        internal readonly byte* _data;

        /// <summary>
        /// Attach a view to an unmanaged buffer owned by external code
        /// </summary>
        /// <param name="data">Unmanaged byte buffer</param>
        /// <param name="length">Length of the buffer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(long length, IntPtr data)
        {
            if (data == IntPtr.Zero)
            {
                ThrowHelper.ThrowArgumentNullException("data");
            }
            if (length <= 0)
            {
                ThrowHelper.ThrowArgumentException("Memory size must be > 0");
            }
            _length = length;
            _data = (byte*)data;
        }

        /// <summary>
        /// Unsafe constructors performs no input checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(long length, byte* data)
        {
            _length = length;
            _data = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(Span<byte> span)
        {
            _length = span.Length;
            _data = (byte*)AsPointer(ref MemoryMarshal.GetReference(span));
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _length > 0 && (IntPtr) _data != IntPtr.Zero; }
        }

        public Span<byte> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new Span<byte>(_data, (int)_length); }
        }

        /// <summary>
        /// Capacity of the underlying buffer
        /// </summary>
        public long Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _length; }
        }

        public IntPtr Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (IntPtr)_data; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer Slice(long start)
        {
            return new DirectBuffer(_length - start, (IntPtr)(Data.ToInt64() + start));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer Slice(long start, long length)
        {
            if (!HasCapacity(start, length))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }
            return new DirectBuffer(length, (IntPtr)(Data.ToInt64() + start));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasCapacity(long offset, long length)
        {
            return (ulong)offset + (ulong)length <= (ulong)_length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Assert(long index, long length)
        {
            if (Settings.AdditionalCorrectnessChecks.Enabled)
            {
                // NB Not FailFast because we do not modify data on failed checks
                if (!IsValid)
                {
                    ThrowHelper.ThrowInvalidOperationException("DirectBuffer is invalid");
                }
                if ((ulong)index + (ulong)length > (ulong)_length)
                {
                    ThrowHelper.ThrowArgumentException("Not enough space in DirectBuffer");
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index">index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar(long index)
        {
            Assert(index, 2);
            return ReadUnaligned<char>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(long index, char value)
        {
            Assert(index, 2);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte(long index)
        {
            Assert(index, 1);
            return ReadUnaligned<sbyte>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(long index, sbyte value)
        {
            Assert(index, 1);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte(long index)
        {
            Assert(index, 1);
            return ReadUnaligned<byte>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(long index, byte value)
        {
            Assert(index, 1);
            WriteUnaligned(_data + index, value);
        }

        public byte this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Assert(index, 1);
                return ReadUnaligned<byte>(_data + index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Assert(index, 1);
                WriteUnaligned(_data + index, value);
            }
        }

        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16(long index)
        {
            Assert(index, 2);
            return ReadUnaligned<short>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(long index, short value)
        {
            Assert(index, 2);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32(long index)
        {
            Assert(index, 4);
            return ReadUnaligned<int>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(long index, int value)
        {
            Assert(index, 4);
            WriteUnaligned(_data + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int VolatileReadInt32(long index)
        {
            Assert(index, 4);
            return Volatile.Read(ref *(int*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteInt32(long index, int value)
        {
            Assert(index, 4);
            Volatile.Write(ref *(int*)(new IntPtr(_data + index)), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint VolatileReadUInt32(long index)
        {
            Assert(index, 4);
            return Volatile.Read(ref *(uint*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteUInt32(long index, uint value)
        {
            Assert(index, 4);
            Volatile.Write(ref *(uint*)(new IntPtr(_data + index)), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long VolatileReadInt64(long index)
        {
            Assert(index, 8);
            return Volatile.Read(ref *(long*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteUInt64(long index, ulong value)
        {
            Assert(index, 8);
            Volatile.Write(ref *(ulong*)(new IntPtr(_data + index)), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong VolatileReadUInt64(long index)
        {
            Assert(index, 8);
            return Volatile.Read(ref *(ulong*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteInt64(long index, long value)
        {
            Assert(index, 8);
            Volatile.Write(ref *(long*)(new IntPtr(_data + index)), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int InterlockedIncrementInt32(long index)
        {
            Assert(index, 4);
            return Interlocked.Increment(ref *(int*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int InterlockedDecrementInt32(long index)
        {
            Assert(index, 4);
            return Interlocked.Decrement(ref *(int*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int InterlockedAddInt32(long index, int value)
        {
            Assert(index, 4);
            return Interlocked.Add(ref *(int*)(new IntPtr(_data + index)), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int InterlockedReadInt32(long index)
        {
            Assert(index, 4);
            return Interlocked.Add(ref *(int*)(new IntPtr(_data + index)), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int InterlockedCompareExchangeInt32(long index, int value, int comparand)
        {
            Assert(index, 4);
            return Interlocked.CompareExchange(ref *(int*)(new IntPtr(_data + index)), value, comparand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long InterlockedIncrementInt64(long index)
        {
            Assert(index, 8);
            return Interlocked.Increment(ref *(long*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long InterlockedDecrementInt64(long index)
        {
            Assert(index, 8);
            return Interlocked.Decrement(ref *(long*)(new IntPtr(_data + index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long InterlockedAddInt64(long index, long value)
        {
            Assert(index, 8);
            return Interlocked.Add(ref *(long*)(new IntPtr(_data + index)), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long InterlockedReadInt64(long index)
        {
            Assert(index, 8);
            return Interlocked.Add(ref *(long*)(new IntPtr(_data + index)), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long InterlockedCompareExchangeInt64(long index, long value, long comparand)
        {
            Assert(index, 8);
            return Interlocked.CompareExchange(ref *(long*)(new IntPtr(_data + index)), value, comparand);
        }

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64(long index)
        {
            Assert(index, 8);
            return ReadUnaligned<long>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long index, long value)
        {
            Assert(index, 8);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16(long index)
        {
            Assert(index, 2);
            return ReadUnaligned<ushort>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(long index, ushort value)
        {
            Assert(index, 2);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32(long index)
        {
            Assert(index, 4);
            return ReadUnaligned<uint>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(long index, uint value)
        {
            Assert(index, 4);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64(long index)
        {
            Assert(index, 8);
            return ReadUnaligned<ulong>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUInt64(long index, ulong value)
        {
            Assert(index, 8);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadFloat(long index)
        {
            Assert(index, 4);
            return ReadUnaligned<float>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(long index, float value)
        {
            Assert(index, 4);
            WriteUnaligned(_data + index, value);
        }

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble(long index)
        {
            Assert(index, 8);
            return ReadUnaligned<double>(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(long index, double value)
        {
            Assert(index, 8);
            WriteUnaligned(_data + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>(long index)
        {
            var size = SizeOf<T>();
            Assert(index, size);
            return ReadUnaligned<T>(_data + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read<T>(long index, out T value)
        {
            var size = SizeOf<T>();
            Assert(index, size);
            value = ReadUnaligned<T>(_data + index);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(long index, T value)
        {
            Assert(index, SizeOf<T>());
            WriteUnaligned(_data + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(long index, int length)
        {
            Assert(index, length);
            var destination = new IntPtr(_data + index);
            Unsafe.InitBlockUnaligned((void*)destination, 0, (uint)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once InconsistentNaming
        public UUID ReadUUID(long index)
        {
            Assert(index, 16);
            return ReadUnaligned<UUID>(_data + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once InconsistentNaming
        public void WriteUUID(long index, UUID value)
        {
            Assert(index, 16);
            WriteUnaligned(_data + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadAsciiDigit(long index)
        {
            Assert(index, 1);
            return ReadUnaligned<byte>(_data + index) - '0';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAsciiDigit(long index, byte value)
        {
            Assert(index, 1);
            WriteUnaligned(_data + index, (byte)(value + '0'));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VerifyAlignment(int alignment)
        {
            if (0 != ((long)_data & (alignment - 1)))
            {
                ThrowHelper.ThrowInvalidOperationException(
                    $"DirectBuffer is not correctly aligned: addressOffset={(long)_data:D} in not divisible by {alignment:D}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(long index, Memory<byte> destination, int length)
        {
            if ((ulong)destination.Length < (ulong)length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }

            fixed (byte* destPtr = &MemoryMarshal.GetReference(destination.Span))
            {
                CopyTo(index, (IntPtr)destPtr, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(long index, DirectBuffer destination, int destinationOffset, int length)
        {
            destination.Assert(destinationOffset, length);
            CopyTo(index, destination.Data + destinationOffset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(long index, IntPtr destination, int length)
        {
            Assert(index, length);
            CopyBlockUnaligned((byte*)destination, _data + index, checked((uint)length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(long index, ReadOnlyMemory<byte> source, int length)
        {
            if ((ulong)source.Length < (ulong)length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }

            fixed (byte* srcPtr = &MemoryMarshal.GetReference(source.Span))
            {
                CopyFrom(index, (IntPtr)srcPtr, length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(long index, DirectBuffer source, int sourceOffset, int length)
        {
            source.Assert(sourceOffset, length);
            CopyFrom(index, source.Data + sourceOffset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(long index, IntPtr source, int length)
        {
            Assert(index, length);
            CopyBlockUnaligned(_data + index, (byte*)source, checked((uint)length));
        }

        ///// <summary>
        ///// Copy this buffer to a pointer
        ///// </summary>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void Copy(IntPtr destination, long srcOffset, long length)
        //{
        //    Assert(srcOffset, length);
        //    CopyBlockUnaligned((byte*)destination, _data + srcOffset, checked((uint)length));
        //}

        ///// <summary>
        ///// Copy this buffer to a byte array
        ///// </summary>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void Copy(byte[] destination, int destOffset, int srcOffset, int length)
        //{
        //    if ((ulong)destOffset + (ulong)length > (ulong)destination.Length)
        //    {
        //        ThrowHelper.ThrowArgumentOutOfRangeException();
        //    }
        //    fixed (byte* dstPtr = &destination[destOffset])
        //    {
        //        Copy((IntPtr)dstPtr, srcOffset, length);
        //    }
        //}

        ///// <summary>
        ///// Copies a range of bytes from the underlying into a supplied byte array.
        ///// </summary>
        ///// <param name="index">index  in the underlying buffer to start from.</param>
        ///// <param name="destination">array into which the bytes will be copied.</param>
        ///// <param name="destinationOffset">offset in the supplied buffer to start the copy</param>
        ///// <param name="length">length of the supplied buffer to use.</param>
        ///// <returns>count of bytes copied.</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int CopyTo(long index, byte[] destination, int destinationOffset, int length)
        //{
        //    if (length > _length - index) throw new ArgumentException("length > _capacity - index");
        //    Marshal.Copy(new IntPtr(_data.ToInt64() + index), destination, destinationOffset, length);
        //    return length;
        //}

        ///// <summary>
        ///// Writes a byte array into the underlying buffer.
        ///// </summary>
        ///// <param name="index">index  in the underlying buffer to start from.</param>
        ///// <param name="source">source byte array to be copied to the underlying buffer.</param>
        ///// <param name="srcOffset">offset in the supplied buffer to begin the copy.</param>
        ///// <param name="length">length of the supplied buffer to copy.</param>
        ///// <returns>count of bytes copied.</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int CopyFrom(long index, byte[] source, int srcOffset, int length)
        //{
        //    Assert(index, length);
        //    if (source.Length < srcOffset + length)
        //    {
        //        ThrowHelper.ThrowArgumentOutOfRangeException();
        //    }
        //    //int count = Math.Min(len, (int)(this._length - index));
        //    Marshal.Copy(source, srcOffset, new IntPtr(_data.ToInt64() + index), length);

        //    return length;
        //}

        ////[MethodImpl(MethodImplOptions.AggressiveInlining)]
        ////public int Write<T>(long index, T value, MemoryStream ms = null)
        ////{
        ////    return Serialization.BinarySerializer.Write<T>(value, ref this, checked((uint)index), ms);
        ////}

        ///// <summary>
        ///// Writes len bytes from source to this buffer at index. Works as memcpy, not memmove.
        ///// </summary>
        ///// <param name="index"></param>
        ///// <param name="src"></param>
        ///// <param name="srcOffset"></param>
        ///// <param name="length"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void WriteBytes(long index, DirectBuffer src, long srcOffset, long length)
        //{
        //    Assert(index, length);
        //    if ((ulong)src._length < (ulong)srcOffset + (ulong)length)
        //    {
        //        ThrowHelper.ThrowArgumentOutOfRangeException();
        //    }
        //    var source = src._data + srcOffset;
        //    var destination = _data + index;
        //    CopyBlockUnaligned(destination, source, checked((uint)length));
        //}
    }
}
