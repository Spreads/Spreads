/*
    Copyright(c) 2014-2015 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization {

    // TODO for convenience, all methods should be public
    // but we must draw the line where we do bound checks on unsafe code and where we don't
    // we should not protect ourselves from shooting in the leg just by hiding the guns

    /// <summary>
    /// Provides unsafe read/write opertaions on a memory pointer. Read/Write arguments are 
    /// not checked for bounds/ranges/overflows.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DirectBuffer : IDirectBuffer
    {
        private readonly long _length;
        private IntPtr _data;

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
        private void Assert(int index, int length) {
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
        public char ReadChar(int index) {
            Assert(index, 1);
            return *((char*)_data + index);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteChar(int index, char value) {
            Assert(index, 1);
            *((byte*)_data + index) = (byte)value;
        }

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public sbyte ReadSByte(int index) {
            Assert(index, 1);
            return *(sbyte*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteSByte(int index, sbyte value) {
            *(sbyte*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public byte ReadByte(int index) {
            Assert(index, 1);
            return *((byte*)_data + index);
        }


        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteByte(int index, byte value) {
            Assert(index, 1);
            *((byte*)_data + index) = value;
        }

        public byte this[int index] {
            get
            {
                Assert(index, 1);
                return *((byte*)_data + index);
            }
            set
            {
                Assert(index, 1);
                *((byte*)_data + index) = value;
            }
        }


        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public short ReadInt16(int index) {
            Assert(index, 2);
            return *(short*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt16(int index, short value) {
            Assert(index, 2);
            *(short*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public int ReadInt32(int index) {
            Assert(index, 4);
            return *(int*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt32(int index, int value) {
            Assert(index, 4);
            *(int*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public long ReadInt64(int index) {
            return *(long*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt64(int index, long value) {
            Assert(index, 8);
            *(long*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ushort ReadUint16(int index) {
            Assert(index, 8);
            return *(ushort*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint16(int index, ushort value) {
            Assert(index, 2);
            *(ushort*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public uint ReadUint32(int index) {
            Assert(index, 2);
            return *(uint*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint32(int index, uint value) {
            Assert(index, 4);
            *(uint*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ulong ReadUint64(int index) {
            Assert(index, 4);
            return *(ulong*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint64(int index, ulong value) {
            Assert(index, 8);
            *(ulong*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public float ReadFloat(int index) {
            Assert(index, 8);
            return *(float*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteFloat(int index, float value) {
            Assert(index, 4);
            *(float*)(_data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public double ReadDouble(int index) {
            Assert(index, 8);
            return *(double*)(_data + index);
        }

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteDouble(int index, double value) {
            Assert(index, 8);
            *(double*)(_data + index) = value;
        }


        /// <summary>
        /// Copies a range of bytes from the underlying into a supplied byte array.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="destination">array into which the bytes will be copied.</param>
        /// <param name="offsetDestination">offset in the supplied buffer to start the copy</param>
        /// <param name="len">length of the supplied buffer to use.</param>
        /// <returns>count of bytes copied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // TODO test if that has an impact
        public int ReadBytes(int index, byte[] destination, int offsetDestination, int len) {
            if (len > this._length - index) throw new ArgumentException("length > _capacity - index");
            Marshal.Copy(_data + index, destination, offsetDestination, len);
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
        /// <param name="offset">offset in the supplied buffer to begin the copy.</param>
        /// <param name="len">length of the supplied buffer to copy.</param>
        /// <returns>count of bytes copied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // TODO test if that has an impact
        public int WriteBytes(int index, byte[] src, int offset, int len) {
            Assert(index, len);
            int count = Math.Min(len, (int)this._length - index);
            Marshal.Copy(src, offset, _data + index, count);

            return count;
        }

        public UUID ReadUUID(int index) {
            Assert(index, 16);
            return *(UUID*)(_data + index);
            //return new UUID(*(ulong*)(_pBuffer + index), *(ulong*)(_pBuffer + index + 8));
        }

        public void WriteUUID(int index, UUID value) {
            Assert(index, 16);
            *(UUID*)(_data + index) = value;
        }

        public int ReadAsciiDigit(int index) {
            Assert(index, 1);
            return (*((byte*)_data + index)) - '0';
        }

        public void WriteAsciiDigit(int index, int value) {
            Assert(index, 1);
            *(byte*)(_data + index) = (byte)(value + '0');
        }

        // TODO add Ascii dates/ints/etc, could take fast implementation from Jil
        // See TAQParse example for ulong and times manual parsing


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SafeBuffer CreateSafeBuffer() {
            return new SafeDirectBuffer(ref this);
        }


        internal sealed unsafe class SafeDirectBuffer : SafeBuffer {
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