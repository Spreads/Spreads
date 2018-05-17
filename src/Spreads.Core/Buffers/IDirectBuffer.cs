// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    public interface IDirectBuffer
    {
        /// <summary>
        /// Capacity of the underlying buffer
        /// </summary>
        long Length { get; }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index">index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        char ReadChar(long index);

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteChar(long index, char value);

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        sbyte ReadSByte(long index);

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteSByte(long index, sbyte value);

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        byte ReadByte(long index);

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteByte(long index, byte value);

        byte this[long index] { get; set; }

        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        short ReadInt16(long index);

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteInt16(long index, short value);

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        int ReadInt32(long index);

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteInt32(long index, int value);

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        long ReadInt64(long index);

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteInt64(long index, long value);

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        ushort ReadUint16(long index);

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteUint16(long index, ushort value);

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        uint ReadUint32(long index);

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteUint32(long index, uint value);

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        ulong ReadUint64(long index);

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteUint64(long index, ulong value);

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        float ReadFloat(long index);

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteFloat(long index, float value);

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        double ReadDouble(long index);

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        void WriteDouble(long index, double value);

        /// <summary>
        /// Copies a range of bytes from the underlying into a supplied byte array.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="destination">array into which the bytes will be copied.</param>
        /// <param name="offsetDestination">offset in the supplied buffer to start the copy</param>
        /// <param name="len">length of the supplied buffer to use.</param>
        /// <returns>count of bytes copied.</returns>
// TODO test if that has an impact
        int ReadBytes(long index, byte[] destination, int offsetDestination, int len);

        int ReadAllBytes(byte[] destination);

        /// <summary>
        /// Writes a byte array into the underlying buffer.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="src">source byte array to be copied to the underlying buffer.</param>
        /// <param name="offset">offset in the supplied buffer to begin the copy.</param>
        /// <param name="len">length of the supplied buffer to copy.</param>
        /// <returns>count of bytes copied.</returns>
// TODO test if that has an impact
        int WriteBytes(long index, byte[] src, int offset, int len);

        // ReSharper disable once InconsistentNaming
        UUID ReadUUID(long index);

        // ReSharper disable once InconsistentNaming
        void WriteUUID(long index, UUID value);

        int ReadAsciiDigit(long index);

        void WriteAsciiDigit(long index, int value);

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        SafeBuffer CreateSafeBuffer();

        /// <summary>
        /// Copy this buffer sto a pointer
        /// </summary>
        void Copy(IntPtr destination, long srcOffset, long length);

        /// <summary>
        /// Copy this buffer to a byte array
        /// </summary>
        void Copy(byte[] destination, int srcOffset, int destOffset, int length);

        /// <summary>
        /// Copy data and move the buffer to the new location
        /// </summary>
        IDirectBuffer Move(IntPtr destination, long srcOffset, long length);

        /// <summary>
        /// Copy data and move the buffer to the new location
        /// </summary>
        IDirectBuffer Move(byte[] destination, int srcOffset, int destOffset, int length);

        int InterlockedIncrementInt32(long index);

        int InterlockedDecrementInt32(long index);

        int InterlockedAddInt32(long index, int value);

        int InterlockedReadInt32(long index);

        int InterlockedCompareExchangeInt32(long index, int value, int comparand);

        long InterlockedIncrementInt64(long index);

        long InterlockedDecrementInt64(long index);

        long InterlockedAddInt64(long index, long value);

        long InterlockedReadInt64(long index);

        long InterlockedCompareExchangeInt64(long index, long value, long comparand);

        int VolatileReadInt32(long index);

        void VolatileWriteInt32(long index, int value);

        long VolatileReadInt64(long index);

        void VolatileWriteInt64(long index, long value);
    }
}