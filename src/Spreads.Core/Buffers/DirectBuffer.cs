// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Buffers
{
    /// <summary>
    /// Provides unsafe read/write operations on a memory pointer.
    /// </summary>
    [DebuggerTypeProxy(typeof(SpreadsBuffers_DirectBufferDebugView))]
    [DebuggerDisplay("Length={" + nameof(Length) + ("}"))]
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct DirectBuffer : IEquatable<DirectBuffer> // TODO , ISegment<DirectBuffer, byte>
    {
        public static DirectBuffer Invalid = new((nint)(-1L), pointer: null);

        /// <summary>
        /// Create a <see cref="DirectBuffer"/> with the null data pointer and the specified length.
        /// This is useful for some byref native APIs.
        /// </summary>
        public static DirectBuffer LengthOnly(uint length) => new(length, pointer: null);

        internal readonly nint _length;
        internal readonly byte* _pointer;

        /// <summary>
        ///
        /// </summary>
        /// <param name="length"></param>
        /// <param name="data"></param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(long length, nint data)
        {
            if (length < 0)
                ThrowHelper.ThrowArgumentException("length must be non negative");

            if (data == default)
                ThrowHelper.ThrowArgumentNullException("data pointer must not be null");

            _length = (nint)length;
            _pointer = (byte*)data;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="length"></param>
        /// <param name="handle"></param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(long length, MemoryHandle handle) : this(length, (nint)handle.Pointer)
        {
        }

        /// <summary>
        /// Unsafe constructors performs no input checks.
        /// </summary>
        /// <param name="length"></param>
        /// <param name="pointer"></param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DirectBuffer(nint length, byte* pointer)
        {
            _length = length;
            _pointer = pointer;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DirectBuffer(long length, byte* pointer)
        {
            _length = (nint)length;
            _pointer = pointer;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="retainedMemory"></param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(RetainedMemory<byte> retainedMemory)
        {
            if (!retainedMemory.IsPinned) ThrowRetainedMemoryNotPinned();

            _length = retainedMemory.Length;
            _pointer = (byte*)retainedMemory.Pointer;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowRetainedMemoryNotPinned()
        {
            ThrowHelper.ThrowInvalidOperationException("RetainedMemory must be pinned for using as DirectBuffer.");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="span">Span must be backed by already pinned memory.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer(Span<byte> span)
        {
            _length = span.Length;
            _pointer = (byte*)AsPointer(ref span.GetPinnableReference());
        }

        public bool IsValid
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer != null;
        }

        public bool IsEmpty
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == default;
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFilledWithValue(byte value)
        {
            return IsFilledWithValue(ref *Data, (ulong)_length, value);
        }

        public Span<byte> Span
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if BUILTIN_SPAN
                return MemoryMarshal.CreateSpan(ref *_pointer, (int)_length);
#else
                return new Span<byte>(_pointer, (int) _length);
#endif
            }
        }

        public int Length
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => checked((int)(long)_length);
        }

        public long LongLength
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public byte* Data
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer;
        }

        /// <summary>
        /// For cases when unsafe is not allowed, e.g. async
        /// </summary>
        public nint DataIntPtr
        {
            [DebuggerStepThrough]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (nint)_pointer;
        }

        [Pure]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer Slice(long start)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(0, start);

            return new DirectBuffer(_length - start, _pointer + start);
        }

        [Pure]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DirectBuffer Slice(long start, long length)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(start, length);

            return new DirectBuffer(length, _pointer + start);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Assert(long index, long length)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (!IsValid)
                    ThrowHelper.ThrowInvalidOperationException("DirectBuffer is invalid.");

                unchecked
                {
                    if ((ulong)index + (ulong)length > (ulong)_length)
                        ThrowHelper.ThrowArgumentException("Not enough space in DirectBuffer or bad index/length.");
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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 2);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 2);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

            WriteUnaligned(_pointer + index, value);
        }

        [Pure]
        public byte this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

                return *(_pointer + index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

                *(_pointer + index) = value;
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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 2);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 2);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            WriteUnaligned(_pointer + index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int VolatileReadInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Volatile.Read(ref *(int*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteInt32(long index, int value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            Volatile.Write(ref *(int*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public uint VolatileReadUInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Volatile.Read(ref *(uint*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteUInt32(long index, uint value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            Volatile.Write(ref *(uint*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long VolatileReadInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return Volatile.Read(ref *(long*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteUInt64(long index, ulong value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            Volatile.Write(ref *(ulong*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public ulong VolatileReadUInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return Volatile.Read(ref *(ulong*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VolatileWriteInt64(long index, long value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            Volatile.Write(ref *(long*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedIncrementInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Interlocked.Increment(ref *(int*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedDecrementInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Interlocked.Decrement(ref *(int*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedAddInt32(long index, int value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Interlocked.Add(ref *(int*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedReadInt32(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Interlocked.Add(ref *(int*)(_pointer + index), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int InterlockedCompareExchangeInt32(long index, int value, int comparand)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

            return Interlocked.CompareExchange(ref *(int*)(_pointer + index), value, comparand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedIncrementInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return Interlocked.Increment(ref *(long*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedDecrementInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return Interlocked.Decrement(ref *(long*)(_pointer + index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedAddInt64(long index, long value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return Interlocked.Add(ref *(long*)(_pointer + index), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedReadInt64(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return Interlocked.Add(ref *(long*)(_pointer + index), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public long InterlockedCompareExchangeInt64(long index, long value, long comparand)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 2);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 2);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            return ReadUnaligned<ulong>(_pointer + index);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> value to a given index.
        /// </summary>
        /// <param name="index">index in bytes for where to put.</param>
        /// <param name="value">value to be written</param>
        public void WriteUInt64(long index, ulong value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 4);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

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
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 8);

            WriteUnaligned(_pointer + index, value);
        }

#pragma warning disable 1574
#pragma warning disable 1584

        /// <summary>
        /// Unaligned read starting from index.
        /// A shortcut to <see cref="Unsafe.ReadUnaligned{T}(void*)"/>.
        /// </summary>
        [Pure]
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, SizeOf<T>());

            return ReadUnaligned<T>(_pointer + index);
        }

#pragma warning restore 1574
#pragma warning restore 1584

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(long index, T value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, SizeOf<T>());

            WriteUnaligned(_pointer + index, value);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int WriteS<T>(long index, T value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, SizeOf<T>());

            WriteUnaligned(_pointer + index, value);
            return SizeOf<T>();
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(long index, int length)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, length);

            var destination = _pointer + index;
            InitBlockUnaligned(destination, 0, (uint)length);
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fill(long index, int length, byte value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, length);

            var destination = _pointer + index;
            InitBlockUnaligned(destination, value, (uint)length);
        }

#if SPREADS

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid ReadGuid(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 16);

            return ReadUnaligned<Guid>(_pointer + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGuid(long index, Guid value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 16);

            WriteUnaligned(_pointer + index, value);
        }

#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public int ReadAsciiDigit(long index)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

            return ReadUnaligned<byte>(_pointer + index) - '0';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAsciiDigit(long index, byte value)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, 1);

            WriteUnaligned(_pointer + index, (byte)(value + '0'));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
        public void VerifyAlignment(int alignment)
        {
            if (0 != ((long)_pointer & (alignment - 1)))
                ThrowHelper.ThrowInvalidOperationException(
                    $"DirectBuffer is not correctly aligned: addressOffset={(long)_pointer:D} in not divisible by {alignment:D}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(ref Memory<byte> destination)
        {
            Span.CopyTo(destination.Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(in DirectBuffer destination)
        {
            if (AdditionalCorrectnessChecks.Enabled) destination.Assert(0, (long)_length);

            CopyTo(0, destination.Data, (int)_length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(long index, void* destination, int length)
        {
            if (AdditionalCorrectnessChecks.Enabled) Assert(index, length);

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

            public byte* Data => _db.Data;

            public long Length => _db.Length;

            public bool IsValid => _db.IsValid;

            public Span<byte> Span => _db.IsValid ? _db.Span : default;
        }

        #endregion Debugger proxy class

        public override int GetHashCode()
        {
#if SPREADS
            return unchecked((int)Algorithms.Hash.Crc32C.CalculateCrc32C(_pointer, checked((int)(long)_length)));
#else
            throw new NotSupportedException();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DirectBuffer other)
        {
            if (_length != other._length)
                return false;

            return SequenceEqual(ref *_pointer, ref *other._pointer, checked((uint)(long)_length));
        }

        // Methods from https://source.dot.net/#System.Private.CoreLib/shared/System/SpanHelpers.Byte.cs,ae8b63bad07668b3

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static nuint LoadUIntPtr(ref byte start, nint offset)
            => ReadUnaligned<UIntPtr>(ref AddByteOffset(ref start, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<byte> LoadVector(ref byte start, nint offset)
            => ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref start, offset));

#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static bool SequenceEqual(ref byte first, ref byte second, uint length)
        {
            if (AreSame(ref first, ref second))
            {
                goto Equal;
            }

            nint offset = default; // Use nint for arithmetic to avoid unnecessary 64->32->64 truncations
            nint lengthToExamine = (nint)(void*)length;

            if (Vector.IsHardwareAccelerated && (byte*)lengthToExamine >= (byte*)Vector<byte>.Count)
            {
                lengthToExamine -= Vector<byte>.Count;
                while ((byte*)lengthToExamine > (byte*)offset)
                {
                    if (LoadVector(ref first, offset) != LoadVector(ref second, offset))
                    {
                        goto NotEqual;
                    }

                    offset += Vector<byte>.Count;
                }

                return LoadVector(ref first, lengthToExamine) == LoadVector(ref second, lengthToExamine);
            }

            if ((byte*)lengthToExamine >= (byte*)sizeof(nuint))
            {
                lengthToExamine -= sizeof(nuint);
                while ((byte*)lengthToExamine > (byte*)offset)
                {
                    if (LoadUIntPtr(ref first, offset) != LoadUIntPtr(ref second, offset))
                    {
                        goto NotEqual;
                    }

                    offset += sizeof(nuint);
                }

                return LoadUIntPtr(ref first, lengthToExamine) == LoadUIntPtr(ref second, lengthToExamine);
            }

            while ((byte*)lengthToExamine > (byte*)offset)
            {
                if (AddByteOffset(ref first, offset) != AddByteOffset(ref second, offset))
                {
                    goto NotEqual;
                }

                offset += 1;
            }

            Equal:
            return true;
            NotEqual: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return false;
        }

#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static bool IsFilledWithValue(ref byte first, ulong length, byte value)
        {
            var valueVector = new Vector<byte>(value);
            nint offset = default; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            nint lengthToExamine = (nint)(void*)length;

            if (Vector.IsHardwareAccelerated && (byte*)lengthToExamine >= (byte*)Vector<byte>.Count)
            {
                lengthToExamine -= Vector<byte>.Count;
                while ((byte*)lengthToExamine > (byte*)offset)
                {
                    if (LoadVector(ref first, offset) != valueVector)
                    {
                        goto NotEqual;
                    }

                    offset += Vector<byte>.Count;
                }

                return LoadVector(ref first, lengthToExamine) == valueVector;
            }

            if ((byte*)lengthToExamine >= (byte*)sizeof(nuint))
            {
                lengthToExamine -= sizeof(nuint);
                nuint uintPtrValue;
                if (UIntPtr.Size == 8)
                {
                    uintPtrValue = (nuint)Vector.AsVectorUInt64(valueVector)[0];
                }
                else
                {
                    uintPtrValue = (nuint)Vector.AsVectorUInt32(valueVector)[0];
                }

                while ((byte*)lengthToExamine > (byte*)offset)
                {
                    if (LoadUIntPtr(ref first, offset) != uintPtrValue)
                    {
                        goto NotEqual;
                    }

                    offset += sizeof(nuint);
                }

                return LoadUIntPtr(ref first, lengthToExamine) == uintPtrValue;
            }

            while ((byte*)lengthToExamine > (byte*)offset)
            {
                if (AddByteOffset(ref first, offset) != value)
                {
                    goto NotEqual;
                }

                offset += 1;
            }

            return true;
            NotEqual: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return false;
        }
    }

    public static unsafe class DirectBufferExtensions
    {
        public static string GetString(this Encoding encoding, in DirectBuffer buffer)
        {
            return encoding.GetString(buffer._pointer, buffer.Length);
        }

        internal static void CopyTo(this ReadOnlySpan<byte> source, DirectBuffer destination)
        {
            fixed (byte* ptr = source)
            {
                var sourceDb = new DirectBuffer(source.Length, ptr);
                sourceDb.CopyTo(destination);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CopyTo(this Span<byte> source, DirectBuffer destination)
        {
            fixed (byte* ptr = source)
            {
                var sourceDb = new DirectBuffer(source.Length, ptr);
                sourceDb.CopyTo(destination);
            }
        }
    }
}
