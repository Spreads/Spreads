// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;
using System.Diagnostics;
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


    // TODO there are WIP changes that was accidentally commited

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol : IEquatable<Symbol>
    {
        private const int Size = 16;
        private fixed byte Bytes[Size];

        [ThreadStatic]
        private static byte[] _buffer1;

        private static byte[] Buffer1 => _buffer1 ?? (_buffer1 = new byte[16]);

        [ThreadStatic]
        private static byte[] _buffer2;

        private static byte[] Buffer2 => _buffer1 ?? (_buffer2 = new byte[16]);

        public Symbol(string symbol)
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

        public bool Equals(Symbol other)
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
            var buffer = BufferPool.StaticBuffer.Buffer;
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
                    buffer.Span[i] = b;
                    len = i + 1;
                }
            }
            // TODO use CoreFxLab new encoding features
            if (buffer.TryGetArray(out var segment))
            {
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, len);
            }
            else
            {
                throw new ApplicationException();
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol && Equals((Symbol)obj);
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

        public static bool operator ==(Symbol x, Symbol y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Symbol x, Symbol y)
        {
            return !x.Equals(y);
        }

        //void Main(string[] args)
        //{
        //    string source = "Hello World!";
        //    using (MD5 md5Hash = MD5.Create())
        //    {
        //        int* block = stackalloc int[100];

        //        byte[] data = md5Hash.TransformBlock(,,,).ComputeHash(Bytes);

        //        string hash = GetMd5Hash(md5Hash, source);

        //        Console.WriteLine("The MD5 hash of " + source + " is: " + hash + ".");

        //        Console.WriteLine("Verifying the hash...");

        //        if (VerifyMd5Hash(md5Hash, source, hash))
        //        {
        //            Console.WriteLine("The hashes are the same.");
        //        }
        //        else
        //        {
        //            Console.WriteLine("The hashes are not same.");
        //        }
        //    }

        // }
    }
}