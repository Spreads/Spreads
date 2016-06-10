/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Serialization {

    // TODO for convenience, all methods should be public
    // but we must draw the line where we do bound checks on unsafe code and where we don't
    // we should not protect ourselves from shooting in the leg just by hiding the guns

    /// <summary>
    /// Provides unsafe read/write opertaions on a memory pointer. Read/Write arguments are 
    /// not checked for bounds/ranges/overflows.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DirectBuffer : IDirectBuffer {
        private readonly long _length;
        internal readonly IntPtr _data;

        /// <summary>
        /// Attach a view to an unmanaged buffer owned by external code
        /// </summary>
        /// <param name="data">Unmanaged byte buffer</param>
        /// <param name="length">Length of the buffer</param>
        public DirectBuffer(long length, IntPtr data) {
            if (data == null) throw new ArgumentNullException("data");
            if (length <= 0) throw new ArgumentException("Buffer size must be > 0", "length");
            this._data = data;
            this._length = length;
        }

        //SafeBuffer
        public DirectBuffer(long length, SafeBuffer buffer) : this(length, PtrFromSafeBuffer(buffer)) {
        }

        private static IntPtr PtrFromSafeBuffer(SafeBuffer buffer) {
            byte* bPtr = null;
            buffer.AcquirePointer(ref bPtr);
            return (IntPtr)bPtr;
        }

        /// <summary>
        /// TODO Move to Bootstrapper
        /// also see this about cpblk http://frankniemeyer.blogspot.de/2014/07/methods-for-reading-structured-binary.html
        /// </summary>
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

        /// <summary>
        /// Copy this buffer to a pointer
        /// </summary>
        public void Copy(IntPtr destination, long srcOffset, long length) {
            memcpy(destination, (IntPtr)(_data.ToInt64() + srcOffset), (UIntPtr)length);
        }

        /// <summary>
        /// Copy this buffer to a byte array
        /// </summary>
        public void Copy(byte[] destination, int srcOffset, int destOffset, int length) {
            Marshal.Copy(_data + srcOffset, destination, destOffset, length);
        }

        /// <summary>
        /// Copy data and move the fixed buffer to the new location
        /// </summary>
        public IDirectBuffer Move(IntPtr destination, long srcOffset, long length) {
            memcpy(destination, (IntPtr)(_data.ToInt64() + srcOffset), (UIntPtr)length);
            return new DirectBuffer(length, destination);
        }

        public IDirectBuffer Move(byte[] destination, int srcOffset, int destOffset, int length) {
            Marshal.Copy(_data + srcOffset, destination, destOffset, length);
            return new FixedBuffer(destination);
        }


        /// <summary>
        /// Capacity of the underlying buffer
        /// </summary>
        public long Length => _length;
        public IntPtr Data => _data;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Assert(long index, long length) {
            if (length <= 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index + length > _length) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (index < 0) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index">index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public char ReadChar(long index) {
            Assert(index, 1);
            return *((char*)new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteChar(long index, char value) {
            Assert(index, 1);
            *((byte*)new IntPtr(_data.ToInt64() + index)) = (byte)value;
        }

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public sbyte ReadSByte(long index) {
            Assert(index, 1);
            return *(sbyte*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteSByte(long index, sbyte value) {
            *(sbyte*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public byte ReadByte(long index) {
            Assert(index, 1);
            return *((byte*)new IntPtr(_data.ToInt64() + index));
        }


        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteByte(long index, byte value) {
            Assert(index, 1);
            *((byte*)new IntPtr(_data.ToInt64() + index)) = value;
        }

        public byte this[long index]
        {
            get
            {
                Assert(index, 1);
                return *((byte*)new IntPtr(_data.ToInt64() + index));
            }
            set
            {
                Assert(index, 1);
                *((byte*)new IntPtr(_data.ToInt64() + index)) = value;
            }
        }


        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public short ReadInt16(long index) {
            Assert(index, 2);
            return *(short*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt16(long index, short value) {
            Assert(index, 2);
            *(short*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public int ReadInt32(long index) {
            Assert(index, 4);
            return *(int*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt32(long index, int value) {
            Assert(index, 4);
            *(int*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        public int VolatileReadInt32(long index) {
            Assert(index, 4);
            return Volatile.Read(ref *(int*)(new IntPtr(_data.ToInt64() + index)));
        }

        public void VolatileWriteInt32(long index, int value) {
            Assert(index, 4);
            Volatile.Write(ref *(int*)(new IntPtr(_data.ToInt64() + index)), value);
        }

        public long VolatileReadInt64(long index) {
            Assert(index, 8);
            return Volatile.Read(ref *(long*)(new IntPtr(_data.ToInt64() + index)));
        }

        public void VolatileWriteInt64(long index, long value) {
            Assert(index, 8);
            Volatile.Write(ref *(long*)(new IntPtr(_data.ToInt64() + index)), value);
        }

        public int InterlockedIncrementInt32(long index) {
            Assert(index, 4);
            return Interlocked.Increment(ref *(int*)(new IntPtr(_data.ToInt64() + index)));
        }

        public int InterlockedDecrementInt32(long index) {
            Assert(index, 4);
            return Interlocked.Decrement(ref *(int*)(new IntPtr(_data.ToInt64() + index)));
        }

        public int InterlockedAddInt32(long index, int value) {
            Assert(index, 4);
            return Interlocked.Add(ref *(int*)(new IntPtr(_data.ToInt64() + index)), value);
        }

        public int InterlockedReadInt32(long index) {
            Assert(index, 4);
            return Interlocked.Add(ref *(int*)(new IntPtr(_data.ToInt64() + index)), 0);
        }

        public int InterlockedCompareExchangeInt32(long index, int value, int comparand) {
            Assert(index, 4);
            return Interlocked.CompareExchange(ref *(int*)(new IntPtr(_data.ToInt64() + index)), value, comparand);
        }

        public long InterlockedIncrementInt64(long index) {
            Assert(index, 8);
            return Interlocked.Increment(ref *(long*)(new IntPtr(_data.ToInt64() + index)));
        }

        public long InterlockedDecrementInt64(long index) {
            Assert(index, 8);
            return Interlocked.Decrement(ref *(long*)(new IntPtr(_data.ToInt64() + index)));
        }

        public long InterlockedAddInt64(long index, long value) {
            Assert(index, 8);
            return Interlocked.Add(ref *(long*)(new IntPtr(_data.ToInt64() + index)), value);
        }

        public long InterlockedReadInt64(long index) {
            Assert(index, 8);
            return Interlocked.Add(ref *(long*)(new IntPtr(_data.ToInt64() + index)), 0);
        }

        public long InterlockedCompareExchangeInt64(long index, long value, long comparand) {
            Assert(index, 8);
            return Interlocked.CompareExchange(ref *(long*)(new IntPtr(_data.ToInt64() + index)), value, comparand);
        }

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public long ReadInt64(long index) {
            return *(long*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt64(long index, long value) {
            Assert(index, 8);
            *(long*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ushort ReadUint16(long index) {
            Assert(index, 8);
            return *(ushort*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint16(long index, ushort value) {
            Assert(index, 2);
            *(ushort*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUint32(long index) {
            Assert(index, 2);
            return *(uint*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUint32(long index, uint value) {
            Assert(index, 4);
            *(uint*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ulong ReadUint64(long index) {
            Assert(index, 4);
            return *(ulong*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint64(long index, ulong value) {
            Assert(index, 8);
            *(ulong*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public float ReadFloat(long index) {
            Assert(index, 8);
            return *(float*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteFloat(long index, float value) {
            Assert(index, 4);
            *(float*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public double ReadDouble(long index) {
            Assert(index, 8);
            return *(double*)(new IntPtr(_data.ToInt64() + index));
        }

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteDouble(long index, double value) {
            Assert(index, 8);
            *(double*)(new IntPtr(_data.ToInt64() + index)) = value;
        }


        /// <summary>
        /// Copies a range of bytes from the underlying into a supplied byte array.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="destination">array into which the bytes will be copied.</param>
        /// <param name="offsetDestination">offset in the supplied buffer to start the copy</param>
        /// <param name="len">length of the supplied buffer to use.</param>
        /// <returns>count of bytes copied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadBytes(long index, byte[] destination, int offsetDestination, int len) {
            if (len > this._length - index) throw new ArgumentException("length > _capacity - index");
            Marshal.Copy(new IntPtr(_data.ToInt64() + index), destination, offsetDestination, len);
            return len;
        }


        public int ReadAllBytes(byte[] destination) {
            if (_length > int.MaxValue) {
                // TODO (low) .NET already supports arrays larger than 2 Gb, 
                // but Marshal.Copy doesn't accept long as a parameter
                // Use memcpy and fixed() over an empty large array
                throw new NotImplementedException("Buffer length is larger than the maximum size of a byte array.");
            } else {
                Marshal.Copy((this._data), destination, 0, (int)_length);
                return (int)_length;
            }
        }

        /// <summary>
        /// Writes a byte array into the underlying buffer.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="src">source byte array to be copied to the underlying buffer.</param>
        /// <param name="srcOffset">offset in the supplied buffer to begin the copy.</param>
        /// <param name="len">length of the supplied buffer to copy.</param>
        /// <returns>count of bytes copied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteBytes(long index, byte[] src, int srcOffset, int len) {
            Assert(index, len);
            if (src.Length < srcOffset + len) throw new ArgumentOutOfRangeException();
            //int count = Math.Min(len, (int)(this._length - index));
            Marshal.Copy(src, srcOffset, new IntPtr(_data.ToInt64() + index), len);

            return len;
        }

        /// <summary>
        /// Writes len bytes from source to this buffer at index. Works as memcpy, not memmove (TODO)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="src"></param>
        /// <param name="srcOffset"></param>
        /// <param name="len"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(long index, DirectBuffer src, long srcOffset, long length) {
            Assert(index, length);
            if (src._length < srcOffset + length) throw new ArgumentOutOfRangeException();

            var pos = 0;
            var source = new IntPtr(src.Data.ToInt64() + srcOffset);
            var destination = _data.ToInt64() + index;
            while (pos < length) {
                int remaining = (int)length - pos;
                if (remaining >= 64) {
                    *(ByteUtil.CopyChunk64*)(destination + pos) = *(ByteUtil.CopyChunk64*)(source + pos);
                    pos += 64;
                    continue;
                }
                if (remaining >= 32) {
                    *(ByteUtil.CopyChunk32*)(destination + pos) = *(ByteUtil.CopyChunk32*)(source + pos);
                    pos += 32;
                    continue;
                }
                if (remaining >= 16) {
                    *(ByteUtil.CopyChunk16*)(destination + pos) = *(ByteUtil.CopyChunk16*)(source + pos);
                    pos += 16;
                    continue;
                }
                if (remaining >= 8) {
                    *(long*)(destination + pos) = *(long*)(source + pos);
                    pos += 8;
                    continue;
                }
                if (remaining >= 4) {
                    *(int*)(destination + pos) = *(int*)(source + pos);
                    pos += 4;
                    continue;
                }
                if (remaining >= 1) {
                    *(byte*)(destination + pos) = *(byte*)(source + pos);
                    pos++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(long index, int length) {
            Assert(index, length);
            var pos = 0;
            var destination = new IntPtr(_data.ToInt64() + index);
            while (pos < length) {
                int remaining = (int)length - pos;
                if (remaining >= 64) {
                    *(ByteUtil.CopyChunk64*)(destination + pos) = (default(ByteUtil.CopyChunk64));
                    pos += 64;
                    continue;
                }
                if (remaining >= 32) {
                    *(ByteUtil.CopyChunk32*)(destination + pos) = (default(ByteUtil.CopyChunk32));
                    pos += 32;
                    continue;
                }
                if (remaining >= 16) {
                    *(ByteUtil.CopyChunk16*)(destination + pos) = (default(ByteUtil.CopyChunk16));
                    pos += 16;
                    continue;
                }
                if (remaining >= 8) {
                    *(long*)(destination + pos) = 0L;
                    pos += 8;
                    continue;
                }
                if (remaining >= 4) {
                    *(int*)(destination + pos) = 0;
                    pos += 4;
                    continue;
                }
                if (remaining >= 1) {
                    *(byte*)(destination + pos) = (byte)(0);
                    pos++;
                }
            }
        }

        public UUID ReadUUID(long index) {
            Assert(index, 16);
            return *(UUID*)(new IntPtr(_data.ToInt64() + index));
            //return new UUID(*(ulong*)(_pBuffer + index), *(ulong*)(_pBuffer + index + 8));
        }

        public void WriteUUID(long index, UUID value) {
            Assert(index, 16);
            *(UUID*)(new IntPtr(_data.ToInt64() + index)) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>(long index) {
            var len = TypeHelper<T>.Size;
            if (len <= 0) {
                Assert(index, 8);
                len = ReadInt32(index + 4);
            }
            Assert(index, len);
            var address = new IntPtr(_data.ToInt64() + index);
            return TypeHelper<T>.PtrToStructure(address);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(long index, T value) {
            MemoryStream stream = null;
            var len = TypeHelper<T>.SizeOf(value, ref stream);
            Assert(index, len);
            var ptr = new IntPtr(_data.ToInt64() + index);
            if (stream == null) {
                TypeHelper<T>.StructureToPtr(value, ptr);
            } else {
                stream.WriteToPtr(new IntPtr(_data.ToInt64() + index));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy<T>(long source, long target) {
            var len = TypeHelper<T>.Size;
            Assert(source, len);
            Assert(target, len);
            var src = new IntPtr(_data.ToInt64() + source);
            var tgt = new IntPtr(_data.ToInt64() + target);
            TypeHelper<T>.PtrToPtr(src, tgt);
        }

        public int ReadAsciiDigit(long index) {
            Assert(index, 1);
            return (*((byte*)new IntPtr(_data.ToInt64() + index))) - '0';
        }

        public void WriteAsciiDigit(long index, int value) {
            Assert(index, 1);
            *(byte*)(new IntPtr(_data.ToInt64() + index)) = (byte)(value + '0');
        }

        public void VerifyAlignment(int alignment) {
            if (0 != (_data.ToInt64() & (alignment - 1))) {
                throw new InvalidOperationException(
                    $"AtomicBuffer is not correctly aligned: addressOffset={_data.ToInt64():D} in not divisible by {alignment:D}");
            }
        }


        // TODO add Ascii dates/ints/etc, could take fast implementation from Jil
        // See TAQParse example for ulong and times manual parsing


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SafeBuffer CreateSafeBuffer() {
            var buffer = this;
            return new SafeDirectBuffer(ref buffer);
        }


        internal sealed class SafeDirectBuffer : SafeBuffer {
            private readonly DirectBuffer _directBuffer;

            public SafeDirectBuffer(ref DirectBuffer directBuffer) : base(false) {
                _directBuffer = directBuffer;
                base.SetHandle(_directBuffer._data);
                base.Initialize((uint)_directBuffer._length);
            }

            protected override bool ReleaseHandle() {
                return true;
            }
        }


    }
}