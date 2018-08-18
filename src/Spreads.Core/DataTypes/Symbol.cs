// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Spreads.DataTypes
{
    // See https://codeblog.jonskeet.uk/2011/04/05/of-memory-and-strings/
    // why this has a lot of sense in some cases: on x64 a string takes 26 + length * 2,
    // so we always win for small strings even with padding.

    /// <summary>
    /// A struct to store up to 16 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Symbol : IEquatable<Symbol>
    {
        private const int Size = 16;
        private fixed byte Bytes[Size];

        /// <summary>
        /// Symbol constructor.
        /// </summary>
        /// <param name="symbol">A string with byte length less or equal to 16.</param>
        public Symbol(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                ThrowArgumentOutOfRangeException(nameof(symbol));
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, Size);
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol other)
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(long*)(ptr) == *(long*)(other.Bytes) && *(long*)((byte*)ptr + 8) == *(long*)(other.Bytes + 8);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var buffer = BufferPool.StaticBuffer.Memory;
            var len = 0;
            var span = buffer.Span;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                span[i] = b;
                len = i + 1;
            }

            // TODO use CoreFxLab new encoding features
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, len);
            }
            else
            {
                ThrowApplicationException();
                return String.Empty;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol && Equals((Symbol)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(int*)ptr;
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(Symbol x, Symbol y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(Symbol x, Symbol y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get Symbol as bytes Span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return new Span<byte>(ptr, Size);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowArgumentOutOfRangeException(string argumentName)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Symbol length is too large");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowApplicationException()
        {
            throw new ApplicationException();
        }
    }

    /// <summary>
    /// A struct to store up to 64 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Symbol32 : IEquatable<Symbol32>
    {
        private const int Size = 32;
        private fixed byte Bytes[Size];

        /// <summary>
        /// Symbol constructor.
        /// </summary>
        public Symbol32(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                ThrowArgumentOutOfRangeException(nameof(symbol));
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, Size);
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol32 other)
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(long*)(ptr) == *(long*)(other.Bytes) && *(long*)((byte*)ptr + 8) == *(long*)(other.Bytes + 8);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var buffer = BufferPool.StaticBuffer.Memory;
            var len = 0;
            var span = buffer.Span;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                span[i] = b;
                len = i + 1;
            }

            // TODO use CoreFxLab new encoding features
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, len);
            }
            else
            {
                ThrowApplicationException();
                return String.Empty;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol32 && Equals((Symbol32)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(int*)ptr;
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(Symbol32 x, Symbol32 y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(Symbol32 x, Symbol32 y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get Symbol as bytes Span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return new Span<byte>(ptr, Size);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowArgumentOutOfRangeException(string argumentName)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Symbol length is too large");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowApplicationException()
        {
            throw new ApplicationException();
        }
    }

    /// <summary>
    /// A struct to store up to 64 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Symbol64 : IEquatable<Symbol64>
    {
        private const int Size = 64;
        private fixed byte Bytes[Size];

        /// <summary>
        /// Symbol constructor.
        /// </summary>
        public Symbol64(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                ThrowArgumentOutOfRangeException(nameof(symbol));
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, Size);
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol64 other)
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(long*)(ptr) == *(long*)(other.Bytes) && *(long*)((byte*)ptr + 8) == *(long*)(other.Bytes + 8);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var buffer = BufferPool.StaticBuffer.Memory;
            var len = 0;
            var span = buffer.Span;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                span[i] = b;
                len = i + 1;
            }

            // TODO use CoreFxLab new encoding features
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, len);
            }
            else
            {
                ThrowApplicationException();
                return String.Empty;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol64 && Equals((Symbol64)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(int*)ptr;
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(Symbol64 x, Symbol64 y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(Symbol64 x, Symbol64 y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get Symbol as bytes Span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return new Span<byte>(ptr, Size);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowArgumentOutOfRangeException(string argumentName)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Symbol length is too large");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowApplicationException()
        {
            throw new ApplicationException();
        }
    }

    /// <summary>
    /// A struct to store up to 128 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Symbol128 : IEquatable<Symbol128>
    {
        private const int Size = 128;
        private fixed byte Bytes[Size];

        /// <summary>
        /// Symbol constructor.
        /// </summary>
        public Symbol128(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                ThrowArgumentOutOfRangeException(nameof(symbol));
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, Size);
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol128 other)
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(long*)(ptr) == *(long*)(other.Bytes) && *(long*)((byte*)ptr + 8) == *(long*)(other.Bytes + 8);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var buffer = BufferPool.StaticBuffer.Memory;
            var len = 0;
            var span = buffer.Span;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                span[i] = b;
                len = i + 1;
            }

            // TODO use CoreFxLab new encoding features
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, len);
            }
            else
            {
                ThrowApplicationException();
                return String.Empty;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol128 && Equals((Symbol128)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(int*)ptr;
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(Symbol128 x, Symbol128 y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(Symbol128 x, Symbol128 y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get Symbol as bytes Span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return new Span<byte>(ptr, Size);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowArgumentOutOfRangeException(string argumentName)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Symbol length is too large");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowApplicationException()
        {
            throw new ApplicationException();
        }
    }

    /// <summary>
    /// A struct to store up to 256 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Symbol256 : IEquatable<Symbol256>
    {
        private const int Size = 256;
        private fixed byte Bytes[Size];

        /// <summary>
        /// Symbol constructor.
        /// </summary>
        public Symbol256(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                ThrowArgumentOutOfRangeException(nameof(symbol));
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, Size);
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol256 other)
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(long*)(ptr) == *(long*)(other.Bytes) && *(long*)((byte*)ptr + 8) == *(long*)(other.Bytes + 8);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var buffer = BufferPool.StaticBuffer.Memory;
            var len = 0;
            var span = buffer.Span;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                span[i] = b;
                len = i + 1;
            }

            // TODO use CoreFxLab new encoding features
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, len);
            }
            else
            {
                ThrowApplicationException();
                return String.Empty;
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol256 && Equals((Symbol256)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return *(int*)ptr;
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(Symbol256 x, Symbol256 y)
        {
            return x.Equals(y);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(Symbol256 x, Symbol256 y)
        {
            return !x.Equals(y);
        }

        /// <summary>
        /// Get Symbol as bytes Span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return new Span<byte>(ptr, Size);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowArgumentOutOfRangeException(string argumentName)
        {
            throw new ArgumentOutOfRangeException(argumentName, "Symbol length is too large");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowApplicationException()
        {
            throw new ApplicationException();
        }
    }
}