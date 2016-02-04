using System;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
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
        unsafe char ReadChar(int index);

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteChar(int index, char value);

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe sbyte ReadSByte(int index);

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteSByte(int index, sbyte value);

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe byte ReadByte(int index);

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteByte(int index, byte value);

        unsafe byte this[int index] { get; set; }

        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe short ReadInt16(int index);

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteInt16(int index, short value);

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe int ReadInt32(int index);

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteInt32(int index, int value);

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe long ReadInt64(int index);

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteInt64(int index, long value);

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe ushort ReadUint16(int index);

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteUint16(int index, ushort value);

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe uint ReadUint32(int index);

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteUint32(int index, uint value);

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe ulong ReadUint64(int index);

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteUint64(int index, ulong value);

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe float ReadFloat(int index);

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteFloat(int index, float value);

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        unsafe double ReadDouble(int index);

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        unsafe void WriteDouble(int index, double value);

        /// <summary>
        /// Copies a range of bytes from the underlying into a supplied byte array.
        /// </summary>
        /// <param name="index">index  in the underlying buffer to start from.</param>
        /// <param name="destination">array into which the bytes will be copied.</param>
        /// <param name="offsetDestination">offset in the supplied buffer to start the copy</param>
        /// <param name="len">length of the supplied buffer to use.</param>
        /// <returns>count of bytes copied.</returns>
// TODO test if that has an impact
        int ReadBytes(int index, byte[] destination, int offsetDestination, int len);

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
        int WriteBytes(int index, byte[] src, int offset, int len);

        unsafe UUID ReadUUID(int index);
        unsafe void WriteUUID(int index, UUID value);
        unsafe int ReadAsciiDigit(int index);
        unsafe void WriteAsciiDigit(int index, int value);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        SafeBuffer CreateSafeBuffer();

        /// <summary>
        /// Copy this buffer sto a pointer
        /// </summary>
        unsafe void Copy(IntPtr destination, long srcOffset, long length);

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

        unsafe int InterlockedIncrementInt32(int index);
        unsafe int InterlockedDecrementInt32(int index);
        unsafe int InterlockedAddInt32(int index, int value);
        unsafe int InterlockedReadInt32(int index);
        unsafe int InterlockedCompareExchangeInt32(int index, int value, int comparand);
        unsafe long InterlockedIncrementInt64(int index);
        unsafe long InterlockedDecrementInt64(int index);
        unsafe long InterlockedAddInt64(int index, long value);
        unsafe long InterlockedReadInt64(int index);
        unsafe long InterlockedCompareExchangeInt64(int index, long value, long comparand);
        unsafe int VolatileReadInt32(int index);
        unsafe void VolatileWriteInt32(int index, int value);
        unsafe int VolatileReadInt64(int index);
        unsafe void VolatileWriteInt64(int index, int value);
    }
}