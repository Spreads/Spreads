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
    public unsafe struct DirectBuffer {
        internal IntPtr length;
        internal IntPtr data;

        /// <summary>
        /// Attach a view to an unmanaged buffer owned by external code
        /// </summary>
        /// <param name="data">Unmanaged byte buffer</param>
        /// <param name="length">Length of the buffer</param>
        public DirectBuffer(int length, IntPtr data) {
            if (data == null) throw new ArgumentNullException("data");
            if (length <= 0) throw new ArgumentException("Buffer size must be > 0", "length");
            this.data = data;
            this.length = (IntPtr)length;
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
        internal void Copy(byte* destination, int srcOffset, int length) {
            memcpy((IntPtr)destination, (IntPtr)(data + srcOffset), (UIntPtr)length);
        }

        /// <summary>
        /// Copy data and move the fixed buffer to the new location
        /// </summary>
        internal DirectBuffer Move(IntPtr destination, int srcOffset, int length) {
            memcpy((IntPtr)destination, (IntPtr)(data + srcOffset), (UIntPtr)length);
            return new DirectBuffer(length, destination);
        }

        internal void Copy(byte[] destination, int srcOffset, int destOffset, int length) {
            Marshal.Copy((IntPtr)data, destination, srcOffset, length);
        }


        /// <summary>
        /// Capacity of the underlying buffer
        /// </summary>
        public int Length {
            get { return length.ToInt32(); }
        }

        public IntPtr Data {
            get { return data; }
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index">index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public char ReadChar(int index) {
            return *((char*)data + index);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteChar(int index, char value) {
            *((byte*)data + index) = (byte)value;
        }

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public sbyte ReadSByte(int index) {
            return *(sbyte*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteSByte(int index, sbyte value) {
            *(sbyte*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public byte ReadByte(int index) {
            return *((byte*)data + index);
        }


        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteByte(int index, byte value) {
            *((byte*)data + index) = value;
        }

        public byte this[int index] {
            get { return *((byte*)data + index); }
            set { *((byte*)data + index) = value; }
        }


        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public short ReadInt16(int index) {
            return *(short*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt16(int index, short value) {
            *(short*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public int ReadInt32(int index) {
            return *(int*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt32(int index, int value) {
            *(int*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public long ReadInt64(int index) {
            return *(long*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt64(int index, long value) {
            *(long*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ushort ReadUint16(int index) {
            return *(ushort*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint16(int index, ushort value) {
            *(ushort*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public uint ReadUint32(int index) {
            return *(uint*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint32(int index, uint value) {
            *(uint*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ulong ReadUint64(int index) {
            return *(ulong*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint64(int index, ulong value) {
            *(ulong*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public float ReadFloat(int index) {
            return *(float*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteFloat(int index, float value) {
            *(float*)(data + index) = value;
        }

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public double ReadDouble(int index) {
            return *(double*)(data + index);
        }

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteDouble(int index, double value) {
            *(double*)(data + index) = value;
        }


        /// <summary>
        /// Copies a range of bytes from the underlying into a supplied byte array.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="destination">array into which the bytes will be copied.</param>
        /// <param name="offsetDestination">offset in the supplied buffer to start the copy</param>
        /// <param name="length">length of the supplied buffer to use.</param>
        /// <returns>count of bytes copied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // TODO test if that has an impact
        public int ReadBytes(int index, byte[] destination, int offsetDestination, int length) {
            if (length > this.length.ToInt32() - index) throw new ArgumentException("length > _capacity - index");
            Marshal.Copy((IntPtr)(data + index), destination, offsetDestination, length);
            return length;
        }


        public int ReadAllBytes(byte[] destination) {
            Marshal.Copy((this.data), destination, 0, length.ToInt32());
            return length.ToInt32();
        }

        /// <summary>
        /// Writes a byte array into the underlying buffer.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="src">source byte array to be copied to the underlying buffer.</param>
        /// <param name="offset">offset in the supplied buffer to begin the copy.</param>
        /// <param name="length">length of the supplied buffer to copy.</param>
        /// <returns>count of bytes copied.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // TODO test if that has an impact
        public int WriteBytes(int index, byte[] src, int offset, int length) {
            int count = Math.Min(length, (int)this.length - index);
            Marshal.Copy(src, offset, (IntPtr)(data + index), count);

            return count;
        }

        public UUID ReadUUID(int index) {
            return *(UUID*)(data + index);
            //return new UUID(*(ulong*)(_pBuffer + index), *(ulong*)(_pBuffer + index + 8));
        }

        public void WriteUUID(int index, UUID value) {
            *(UUID*)(data + index) = value;
        }

        public int ReadAsciiDigit(int index) {
            return (*((byte*)data + index)) - '0';
        }

        public void WriteAsciiDigit(int index, int value) {
            *(byte*)(data + index) = (byte)(value + '0');
        }

        // TODO add Ascii dates/ints/etc, could take fast implementation from Jil
        // See TAQParse example for ulong and times manual parsing
    }


}