// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

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
    [BinarySerialization(Size)]
    public unsafe struct Symbol : IEquatable<Symbol>
    {
        public const int Size = 16;
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
            {
                var ptr = (byte*)Unsafe.AsPointer(ref this);
                Unsafe.InitBlockUnaligned(ptr, 0, Size);
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, byteCount);
            }
        }

        public int ByteLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var span = new Span<byte>(Unsafe.AsPointer(ref Unsafe.As<Symbol, byte>(ref this)), Size);
                var len = span.IndexOf((byte) 0);
                if (len == -1)
                {
                    return Size;
                }

                return len;
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol other)
        {
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            var ptrOther = (byte*)Unsafe.AsPointer(ref other);
            for (int i = 0; i < Size; i += 8)
            {
                if (Unsafe.ReadUnaligned<long>(ptr + i) != Unsafe.ReadUnaligned<long>(ptrOther + i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var len = 0;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                len = i + 1;
            }

            return Encoding.UTF8.GetString(ptr, len);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is Symbol sym && Equals(sym);
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
    }

    /// <summary>
    /// A struct to store up to 64 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(Size)]
    public unsafe struct Symbol32 : IEquatable<Symbol32>
    {
        public const int Size = 32;
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
            {
                var ptr = (byte*)Unsafe.AsPointer(ref this);
                Unsafe.InitBlockUnaligned(ptr, 0, Size);
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, byteCount);
            }
        }

        public int ByteLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var span = new Span<byte>(Unsafe.AsPointer(ref Unsafe.As<Symbol32, byte>(ref this)), Size);
                var len = span.IndexOf((byte)0);
                if (len == -1)
                {
                    return Size;
                }

                return len;
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol32 other)
        {
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            var ptrOther = (byte*)Unsafe.AsPointer(ref other);
            for (int i = 0; i < Size; i += 8)
            {
                if (Unsafe.ReadUnaligned<long>(ptr + i) != Unsafe.ReadUnaligned<long>(ptrOther + i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var len = 0;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                len = i + 1;
            }

            return Encoding.UTF8.GetString(ptr, len);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is Symbol32 sym && Equals(sym);
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
    }

    /// <summary>
    /// A struct to store up to 64 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(Size)]
    public unsafe struct Symbol64 : IEquatable<Symbol64>
    {
        public const int Size = 64;
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
            {
                var ptr = (byte*)Unsafe.AsPointer(ref this);
                Unsafe.InitBlockUnaligned(ptr, 0, Size);
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, byteCount);
            }
        }

        public int ByteLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var span = new Span<byte>(Unsafe.AsPointer(ref Unsafe.As<Symbol64, byte>(ref this)), Size);
                var len = span.IndexOf((byte)0);
                if (len == -1)
                {
                    return Size;
                }

                return len;
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol64 other)
        {
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            var ptrOther = (byte*)Unsafe.AsPointer(ref other);
            for (int i = 0; i < Size; i += 8)
            {
                if (Unsafe.ReadUnaligned<long>(ptr + i) != Unsafe.ReadUnaligned<long>(ptrOther + i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var len = 0;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                len = i + 1;
            }

            return Encoding.UTF8.GetString(ptr, len);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is Symbol64 sym && Equals(sym);
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
    }

    /// <summary>
    /// A struct to store up to 128 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(Size)]
    public unsafe struct Symbol128 : IEquatable<Symbol128>
    {
        public const int Size = 128;
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
            {
                var ptr = (byte*)Unsafe.AsPointer(ref this);
                Unsafe.InitBlockUnaligned(ptr, 0, Size);
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, byteCount);
            }
        }

        public int ByteLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var span = new Span<byte>(Unsafe.AsPointer(ref Unsafe.As<Symbol128, byte>(ref this)), Size);
                var len = span.IndexOf((byte)0);
                if (len == -1)
                {
                    return Size;
                }

                return len;
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol128 other)
        {
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            var ptrOther = (byte*)Unsafe.AsPointer(ref other);
            for (int i = 0; i < Size; i += 8)
            {
                if (Unsafe.ReadUnaligned<long>(ptr + i) != Unsafe.ReadUnaligned<long>(ptrOther + i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var len = 0;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                len = i + 1;
            }

            return Encoding.UTF8.GetString(ptr, len);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is Symbol128 sym && Equals(sym);
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
    }

    /// <summary>
    /// A struct to store up to 256 UTF8 chars.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(Size)]
    public unsafe struct Symbol256 : IEquatable<Symbol256>
    {
        public const int Size = 256;
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
            {
                var ptr = (byte*)Unsafe.AsPointer(ref this);
                Unsafe.InitBlockUnaligned(ptr, 0, Size);
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, ptr, byteCount);
            }
        }

        public int ByteLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var span = new Span<byte>(Unsafe.AsPointer(ref Unsafe.As<Symbol256, byte>(ref this)), Size);
                var len = span.IndexOf((byte)0);
                if (len == -1)
                {
                    return Size;
                }

                return len;
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Symbol256 other)
        {
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            var ptrOther = (byte*)Unsafe.AsPointer(ref other);
            for (int i = 0; i < Size; i += 8)
            {
                if (Unsafe.ReadUnaligned<long>(ptr + i) != Unsafe.ReadUnaligned<long>(ptrOther + i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var len = 0;
            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                len = i + 1;
            }

            return Encoding.UTF8.GetString(ptr, len);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is Symbol256 sym && Equals(sym);
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
    }
}
