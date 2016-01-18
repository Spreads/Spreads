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
using System.Threading;

namespace Spreads.Serialization {

    // TODO pinning of arrays is only needed when
    //  - DirectBuffer property is accessed
    //  - Unmanaged Accessor/Stream is created
    // For all other cases, we could use fixed() which is much kinder to GC
    // TODO add back all read/write methods from DB, and use fixed where possible instead of pinning
    // The Move(*byte) method should work without pinning but using fixed()


    internal class UnpinWhenGCed {
        internal readonly GCHandle PinnedGCHandle;
        public UnpinWhenGCed(GCHandle pinnedGCHandle) {
            PinnedGCHandle = pinnedGCHandle;
        }
        ~UnpinWhenGCed() {
            PinnedGCHandle.Free();
        }
    }

    /// <summary>
    /// Provides read/write opertaions on a byte buffer that is fixed in memory.
    /// </summary>
    public unsafe struct FixedBuffer : IDirectBuffer {

#if PRERELEASE
        static FixedBuffer() {
            if (!BitConverter.IsLittleEndian) {
                // NB we just do not care to support BigEndian. This must be documented. 
                // But it is OK for debugging to leave such a time bomb here.
                // See Aeron docs why BigEndian probably won't be supported even by them.
                throw new NotSupportedException("BigEndian systems are not supported with the current implementation.");
            }
        }
#endif

        private int _offset;
        private int _length;
        private byte[] _buffer;
        private UnpinWhenGCed _unpinner;

        public int Offset => _offset;
        public long Length => _length;
        public byte[] Buffer => _buffer;

        /// <summary>
        /// Attach a view to a byte[] for providing direct access.
        /// </summary>
        /// <param name="buffer">buffer to which the view is attached.</param>
        public FixedBuffer(byte[] buffer, int offset = 0, int length = 0) {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length + offset > buffer.Length) throw new ArgumentException("Length plus offset exceed capacity");

            _offset = 0;
            _length = buffer.Length;
            _buffer = buffer;
            _unpinner = null;
        }

        /// <summary>
        /// Create a new FixedBuffer with a new empty array
        /// </summary>
        /// <param name="length">buffer to which the view is attached.</param>
        public FixedBuffer(int length) {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            _offset = 0;
            _length = length;
            _buffer = OptimizationSettings.ArrayPool.TakeBuffer<byte>(length);
            _unpinner = null;
        }


        private void PinBuffer() {
            // we postpone pinning array as much as possible
            // whenever this struct is copied by value, a reference to
            // unpinner is copied with it, so there is no way to manually unpin
            // other than to GC the unpinner, which happens when the last struct goes out of scope
            _unpinner = new UnpinWhenGCed(GCHandle.Alloc(_buffer, GCHandleType.Pinned));
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
            if (srcOffset + length > _buffer.Length) throw new ArgumentOutOfRangeException("srcOffset + length > _buffer.Length");
            Marshal.Copy(_buffer, (int)srcOffset, destination, (int)length);
        }

        /// <summary>
        /// Copy data and move the fixed buffer to the new location
        /// </summary>
        public IDirectBuffer Move(IntPtr destination, long srcOffset, long length) {
            if (srcOffset + length > _buffer.Length) throw new ArgumentOutOfRangeException("srcOffset + length > _buffer.Length");
            Marshal.Copy(_buffer, (int)srcOffset, destination, (int)length);
            return new DirectBuffer(length, destination);
        }

        public void Copy(byte[] destination, int srcOffset, int destOffset, int length) {
            Array.Copy(_buffer, srcOffset, destination, destOffset, length);
        }

        public IDirectBuffer Move(byte[] destination, int srcOffset, int destOffset, int length) {
            Array.Copy(_buffer, srcOffset, destination, destOffset, length);
            return new FixedBuffer(destination);
        }


        [Obsolete("Unpinner must remain in scope. Use Fixed() method with lambdas instead.")]
        internal DirectBuffer DirectBuffer {
            get {
                PinBuffer();
                return new DirectBuffer(_length, _unpinner.PinnedGCHandle.AddrOfPinnedObject() + _offset);
            }
        }

        /// <summary>
        /// Fix a buffer only for the execution of the func
        /// </summary>
        public T Fixed<T>(Func<DirectBuffer, T> function) {
            fixed (byte* ptr = &_buffer[_offset])
            {
                return function(new DirectBuffer(_length, (IntPtr)ptr));
            }
        }

        /// <summary>
        /// Fix a buffer only for the execution of the func
        /// </summary>
        public void Fixed(Action<DirectBuffer> action) {
            fixed (byte* ptr = &_buffer[_offset])
            {
                action(new DirectBuffer(_length, (IntPtr)ptr));
            }
        }

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
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *((char*)ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteChar(int index, char value) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *((byte*)ptr + index) = (byte)value;
            }
        }

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public sbyte ReadSByte(int index) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(sbyte*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteSByte(int index, sbyte value) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(sbyte*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public byte ReadByte(int index) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *((byte*)ptr + index);
            }
        }


        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteByte(int index, byte value) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *((byte*)ptr + index) = value;
            }
        }

        public byte this[int index] {
            get {
                Assert(index, 1);
                fixed (byte* ptr = &_buffer[_offset])
                {
                    return *((byte*)ptr + index);
                }
            }
            set {
                Assert(index, 1);
                fixed (byte* ptr = &_buffer[_offset])
                {
                    *((byte*)ptr + index) = value;
                }
            }
        }


        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public short ReadInt16(int index) {
            Assert(index, 2);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(short*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt16(int index, short value) {
            Assert(index, 2);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(short*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public int ReadInt32(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(int*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt32(int index, int value) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(int*)(ptr + index) = value;
            }
        }

        public int VolatileReadInt32(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Volatile.Read(ref *(int*)(ptr + index));
            }
        }

        public void VolatileWriteInt32(int index, int value) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                Volatile.Write(ref *(int*)(ptr + index), value);
            }
        }

        public int VolatileReadInt64(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Volatile.Read(ref *(int*)(ptr + index));
            }
        }

        public void VolatileWriteInt64(int index, int value) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                Volatile.Write(ref *(int*)(ptr + index), value);
            }
        }


        public int InterlockedIncrementInt32(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Increment(ref *(int*)(ptr + index));
            }
        }

        public int InterlockedDecrementInt32(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Decrement(ref *(int*)(ptr + index));
            }
        }

        public int InterlockedAddInt32(int index, int value) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Add(ref *(int*)(ptr + index), value);
            }
        }

        public int InterlockedReadInt32(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Add(ref *(int*)(ptr + index), 0);
            }
        }

        public int InterlockedCompareExchangeInt32(int index, int value, int comparand) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.CompareExchange(ref *(int*)(ptr + index), value, comparand);
            }
        }

        public long InterlockedIncrementInt64(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Increment(ref *(long*)(ptr + index));
            }
        }

        public long InterlockedDecrementInt64(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Decrement(ref *(long*)(ptr + index));
            }
        }

        public long InterlockedAddInt64(int index, long value) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Add(ref *(long*)(ptr + index), value);
            }
        }

        public long InterlockedReadInt64(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.Add(ref *(long*)(ptr + index), 0);
            }
        }

        public long InterlockedCompareExchangeInt64(int index, long value, long comparand) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return Interlocked.CompareExchange(ref *(long*)(ptr + index), value, comparand);
            }
        }

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public long ReadInt64(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(long*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteInt64(int index, long value) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(long*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ushort ReadUint16(int index) {
            Assert(index, 2);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(ushort*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint16(int index, ushort value) {
            Assert(index, 2);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(ushort*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public uint ReadUint32(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(uint*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint32(int index, uint value) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(uint*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public ulong ReadUint64(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(ulong*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUint64(int index, ulong value) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(ulong*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public float ReadFloat(int index) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(float*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteFloat(int index, float value) {
            Assert(index, 4);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(float*)(ptr + index) = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        public double ReadDouble(int index) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(double*)(ptr + index);
            }
        }

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteDouble(int index, double value) {
            Assert(index, 8);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(double*)(ptr + index) = value;
            }
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
            fixed (byte* ptr = &_buffer[_offset])
            {
                if (len > this._length - index) throw new ArgumentException("length > _capacity - index");
                Marshal.Copy((IntPtr)ptr + index, destination, offsetDestination, len);
                return len;
            }
        }


        public int ReadAllBytes(byte[] destination) {
            fixed (byte* ptr = &_buffer[_offset])
            {
                if (_length > int.MaxValue) {
                    // TODO (low) .NET already supports arrays larger than 2 Gb, 
                    // but Marshal.Copy doesn't accept long as a parameter
                    // Use memcpy and fixed() over an empty large array
                    throw new NotImplementedException(
                        "Buffer length is larger than the maximum size of a byte array.");
                } else {
                    Marshal.Copy((IntPtr)(ptr), destination, 0, (int)_length);
                    return (int)_length;
                }
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
            fixed (byte* ptr = &_buffer[_offset])
            {
                int count = Math.Min(len, (int)this._length - index);
                Marshal.Copy(src, offset, (IntPtr)ptr + index, count);
                return count;
            }
        }

        public UUID ReadUUID(int index) {
            Assert(index, 16);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return *(UUID*)(ptr + index);
            }
        }

        public void WriteUUID(int index, UUID value) {
            Assert(index, 16);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(UUID*)(ptr + index) = value;
            }
        }

        public int ReadAsciiDigit(int index) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                return (*((byte*)ptr + index)) - '0';
            }
        }

        public void WriteAsciiDigit(int index, int value) {
            Assert(index, 1);
            fixed (byte* ptr = &_buffer[_offset])
            {
                *(byte*)(ptr + index) = (byte)(value + '0');
            }
        }


        // using safe vuffer/accessor could be 2x slower http://ayende.com/blog/163138/memory-mapped-files-file-i-o-performance
        // but it is bound-checked

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SafeBuffer CreateSafeBuffer() {
            return new SafeFixedBuffer(ref this);
        }
        // NB SafeBuffer Read<> is very slow compared to DirectBuffer unsafe methods
        // Use SafeBuffer only when explicitly requested
        internal sealed unsafe class SafeFixedBuffer : SafeBuffer {
            private readonly FixedBuffer _fixedBuffer;

            public SafeFixedBuffer(ref FixedBuffer fixedBuffer) : base(false) {
                _fixedBuffer = fixedBuffer;
                _fixedBuffer.PinBuffer();
                base.SetHandle(_fixedBuffer._unpinner.PinnedGCHandle.AddrOfPinnedObject() + _fixedBuffer._offset);
                base.Initialize((uint)_fixedBuffer._length);
            }

            protected override bool ReleaseHandle() {
                return true;
            }
        }

        //public static implicit operator DirectBuffer(FixedBuffer fixedBuffer) {
        //    return fixedBuffer.DirectBuffer;
        //}
    }
}