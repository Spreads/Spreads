// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Spreads.DataTypes;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Buffers
{
    /// <summary>
    /// Provides unsafe read/write operations on a memory pointer.
    /// </summary>
    [DebuggerTypeProxy(typeof(SpreadsBuffers_DirectBufferDebugView))]
    [DebuggerDisplay("Length={" + nameof(Length) + ("}"))]
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct DirectBuffer
    {
        public static DirectBuffer Invalid = new DirectBuffer(0, (byte*)IntPtr.Zero);

        // NB this is used for Spreads.LMDB as MDB_val, where length is IntPtr. However, LMDB works normally only on x64
        // if we even support x86 we will have to create a DTO with IntPtr length. But for x64 casting IntPtr to/from long
        // is surprisingly expensive, e.g. Slice and ctor show up in profiler.

        internal readonly long _length;
        internal readonly byte* _pointer;

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
            _pointer = (byte*)data;
        }

        /// <summary>
        /// Unsafe constructors performs no input checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(long length, byte* pointer)
        {
            _length = length;
            _pointer = pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(RetainedMemory<byte> retainedMemory)
        {
            _length = retainedMemory.Length;
            _pointer = (byte*)retainedMemory.Pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(Span<byte> span)
        {
            _length = span.Length;
            _pointer = (byte*)AsPointer(ref span.GetPinnableReference());
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer != null;
        }

        // TODO review: empty could be a result from Slice and perfectly valid, so it is not the same as IsValid which checks pointer.
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == 0;
        }

        public Span<byte> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<byte>(_pointer, (int)_length);
        }

        /// <summary>
        /// Capacity of the underlying buffer
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => checked((int)_length);
        }

        public long LongLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public byte* Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer;
        }

        // for cases when unsafe is not allowed, e.g. async
        public IntPtr IntPtr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (IntPtr)_pointer;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer Slice(long start)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(0, start); }

            return new DirectBuffer(_length - start, _pointer + start);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer Slice(long start, long length)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(start, length); }

            return new DirectBuffer(length, _pointer + start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Assert(long index, long length)
        {
            if (AdditionalCorrectnessChecks.Enabled)
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
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 2); }
            return ReadUnaligned<char>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(long index, char value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 2); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="sbyte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 1); }
            return ReadUnaligned<sbyte>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="sbyte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(long index, sbyte value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 1); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="byte"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 1); }
            return ReadUnaligned<byte>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(long index, byte value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 1); }
            WriteUnaligned(_pointer + index, value);
        }

        [Pure]
        public byte this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (AdditionalCorrectnessChecks.Enabled)
                {
                    Assert(index, 1);
                }

                return *(_pointer + index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (AdditionalCorrectnessChecks.Enabled)
                { Assert(index, 1); }

                *(_pointer + index) = value;
            }
        }

        [Pure]
        public ref byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (AdditionalCorrectnessChecks.Enabled)
                {
                    Assert(index, 1);
                }
                return ref AsRef<byte>(*(_pointer + index));
            }
        }

        /// <summary>
        /// Gets the <see cref="short"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public short ReadInt16(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 2); }
            return ReadUnaligned<short>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="short"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(long index, short value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 2); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="int"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int ReadInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return ReadUnaligned<int>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="int"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(long index, int value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            WriteUnaligned(_pointer + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int VolatileReadInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Volatile.Read(ref *(int*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteInt32(long index, int value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            Volatile.Write(ref *(int*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public uint VolatileReadUInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Volatile.Read(ref *(uint*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteUInt32(long index, uint value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            Volatile.Write(ref *(uint*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long VolatileReadInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Volatile.Read(ref *(long*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteUInt64(long index, ulong value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            Volatile.Write(ref *(ulong*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public ulong VolatileReadUInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Volatile.Read(ref *(ulong*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteInt64(long index, long value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            Volatile.Write(ref *(long*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedIncrementInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Interlocked.Increment(ref *(int*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedDecrementInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Interlocked.Decrement(ref *(int*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedAddInt32(long index, int value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Interlocked.Add(ref *(int*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedReadInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Interlocked.Add(ref *(int*)(_pointer + index), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedCompareExchangeInt32(long index, int value, int comparand)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return Interlocked.CompareExchange(ref *(int*)(_pointer + index), value, comparand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedIncrementInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Interlocked.Increment(ref *(long*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedDecrementInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Interlocked.Decrement(ref *(long*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedAddInt64(long index, long value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Interlocked.Add(ref *(long*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedReadInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Interlocked.Add(ref *(long*)(_pointer + index), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedCompareExchangeInt64(long index, long value, long comparand)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return Interlocked.CompareExchange(ref *(long*)(_pointer + index), value, comparand);
        }

        /// <summary>
        /// Gets the <see cref="long"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long ReadInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return ReadUnaligned<long>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="long"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long index, long value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="ushort"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public ushort ReadUInt16(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 2); }
            return ReadUnaligned<ushort>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(long index, ushort value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 2); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="uint"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public uint ReadUInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return ReadUnaligned<uint>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="uint"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(long index, uint value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="ulong"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public ulong ReadUInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return ReadUnaligned<ulong>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUInt64(long index, ulong value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Assert(index, 8);
            }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="float"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public float ReadFloat(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            return ReadUnaligned<float>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="float"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloat(long index, float value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 4); }
            WriteUnaligned(_pointer + index, value);
        }

        /// <summary>
        /// Gets the <see cref="double"/> value at a given index.
        /// </summary>
        /// <param name="index"> index in bytes from which to get.</param>
        /// <returns>the value at a given index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public double ReadDouble(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            return ReadUnaligned<double>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="double"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(long index, double value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 8); }
            WriteUnaligned(_pointer + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public T Read<T>(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                var size = SizeOf<T>();
                Assert(index, size);
            }
            return ReadUnaligned<T>(_pointer + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int Read<T>(long index, out T value)
        {
            var size = SizeOf<T>();
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, size); }
            value = ReadUnaligned<T>(_pointer + index);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(long index, T value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, SizeOf<T>()); }
            WriteUnaligned(_pointer + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(long index, int length)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, length); }
            var destination = _pointer + index;
            InitBlockUnaligned(destination, 0, (uint)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        // ReSharper disable once InconsistentNaming
        public UUID ReadUUID(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 16); }
            return ReadUnaligned<UUID>(_pointer + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once InconsistentNaming
        public void WriteUUID(long index, UUID value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 16); }
            WriteUnaligned(_pointer + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int ReadAsciiDigit(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 1); }
            return ReadUnaligned<byte>(_pointer + index) - '0';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAsciiDigit(long index, byte value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, 1); }
            WriteUnaligned(_pointer + index, (byte)(value + '0'));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VerifyAlignment(int alignment)
        {
            if (0 != ((long)_pointer & (alignment - 1)))
            {
                ThrowHelper.ThrowInvalidOperationException(
                    $"DirectBuffer is not correctly aligned: addressOffset={(long)_pointer:D} in not divisible by {alignment:D}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(ref Memory<byte> destination)
        {
            Span.CopyTo(destination.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(in DirectBuffer destination)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                destination.Assert(0, _length);
            }
            CopyTo(0, destination.Data, (int)_length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(long index, void* destination, int length)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            { Assert(index, length); }
            CopyBlockUnaligned(destination, _pointer + index, checked((uint)length));
        }

        #region Debugger proxy class

        // ReSharper disable once InconsistentNaming
        internal class SpreadsBuffers_DirectBufferDebugView
        {
            private readonly DirectBuffer _db;

            public SpreadsBuffers_DirectBufferDebugView(DirectBuffer db)
            {
                _db = db;
            }

            public void* Data => _db.Data;

            public long Length => _db.Length;

            public bool IsValid => _db.IsValid;

            public Span<byte> Span => _db.IsValid ? _db.Span : default;
        }

        #endregion Debugger proxy class
    }

    public static unsafe class DirectBufferExtensions
    {
        public static string GetString(this Encoding encoding, in DirectBuffer buffer)
        {
            return encoding.GetString(buffer._pointer, buffer.Length);
        }

        /// <summary>
        /// For usage: using (array.AsDirectBuffer(out var db)) { ... }
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle AsDirectBuffer(this byte[] array, out DirectBuffer buffer)
        {
            var mh = ((Memory<byte>)array).Pin();
            buffer = new DirectBuffer(array.Length, (byte*)mh.Pointer);
            return mh;
        }
    }
}
