// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Generated code, edit Symbol16.cs and copy its changes to Symbol.tt to make changes (TODO leave only Symbol.tt)

using System;
using System.Diagnostics;
using System.Text;
using Spreads.Buffers;

namespace Spreads.DataTypes
{
    // See https://codeblog.jonskeet.uk/2011/04/05/of-memory-and-strings/
    // why this has a lot of sense: on x64 a string takes 26 + length * 2,
    // so we always win for small strings even with padding.

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol4 : IEquatable<Symbol4>
    {
        private const int Size = 4;
        private fixed byte Bytes[Size];

        public Symbol4(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol4 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol4 && Equals((Symbol4)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol4 x, Symbol4 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol4 x, Symbol4 y)
        {
            return !x.Equals(y);
        }
    }

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol8 : IEquatable<Symbol8>
    {
        private const int Size = 8;
        private fixed byte Bytes[Size];

        public Symbol8(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol8 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol8 && Equals((Symbol8)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol8 x, Symbol8 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol8 x, Symbol8 y)
        {
            return !x.Equals(y);
        }
    }

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol24 : IEquatable<Symbol24>
    {
        private const int Size = 24;
        private fixed byte Bytes[Size];

        public Symbol24(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol24 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol24 && Equals((Symbol24)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol24 x, Symbol24 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol24 x, Symbol24 y)
        {
            return !x.Equals(y);
        }
    }

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol32 : IEquatable<Symbol32>
    {
        private const int Size = 32;
        private fixed byte Bytes[Size];

        public Symbol32(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol32 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol32 && Equals((Symbol32)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol32 x, Symbol32 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol32 x, Symbol32 y)
        {
            return !x.Equals(y);
        }
    }

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol64 : IEquatable<Symbol64>
    {
        private const int Size = 64;
        private fixed byte Bytes[Size];

        public Symbol64(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol64 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol64 && Equals((Symbol64)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol64 x, Symbol64 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol64 x, Symbol64 y)
        {
            return !x.Equals(y);
        }
    }

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol128 : IEquatable<Symbol128>
    {
        private const int Size = 128;
        private fixed byte Bytes[Size];

        public Symbol128(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol128 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol128 && Equals((Symbol128)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol128 x, Symbol128 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol128 x, Symbol128 y)
        {
            return !x.Equals(y);
        }
    }

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol256 : IEquatable<Symbol256>
    {
        private const int Size = 256;
        private fixed byte Bytes[Size];

        public Symbol256(string symbol)
        {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size)
            {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol256 other)
        {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++)
                {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0)
                    {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol256 && Equals((Symbol256)obj);
        }

        public override int GetHashCode()
        {
            fixed (byte* ptr = Bytes)
            {
                unchecked
                {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++)
                    {
                        var b = *(ptr + i);
                        if (b == 0) break;
                        hash = (hash ^ b) * p;
                    }

                    hash += hash << 13;
                    hash ^= hash >> 7;
                    hash += hash << 3;
                    hash ^= hash >> 17;
                    hash += hash << 5;
                    return hash;
                }
            }
        }

        public static bool operator ==(Symbol256 x, Symbol256 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol256 x, Symbol256 y)
        {
            return !x.Equals(y);
        }
    }
}