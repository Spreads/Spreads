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

    // TODO Watch for usage of this. This is convenient when reading/writing a binary stream from a wire, e.g. when we
    // know that a ticker has max length. But in many cases they could be manually "interned", or there could be a special
    // class Symbol with additional info and a concurrent dictionary that indexes Symbol by string.
    // Then we could store a 8-byte pointer instead of 16-byte symbol (or even an int and have another dictionary). Many tradeoffs...
    // Also see String.Intern method

    /// <summary>
    /// A struct to store up to 16 chars.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Size = Symbol.Size)]
    [Serialization(BlittableSize = Symbol.Size)]
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
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
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
            var buffer = BufferPool.StaticBuffer.Buffer;
            var len = 0;

            var ptr = (byte*)Unsafe.AsPointer(ref this);
            for (int i = 0; i < Size; i++)
            {
                var b = *(ptr + i);
                if (b == 0)
                {
                    break;
                }
                var span = buffer.Span;
                span[i] = b;
                len = i + 1;
            }

            // TODO use CoreFxLab new encoding features
            if (buffer.TryGetArray(out var segment))
            {
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
}