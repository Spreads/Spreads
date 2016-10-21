// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Diagnostics;
using System.Text;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.DataTypes {


    // TODO (low) Symbol8, 32, 64, 128
    // TODO implicit conversion to Symbol128 for all SymbolXXX types
    // Equality then will be resolved automatically to Symbol128

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol16 : IEquatable<Symbol16>
    {
        private const int Size = 16;
        private fixed byte Bytes[Size];

        public Symbol16(string symbol) {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > Size) {
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, Size);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol16 other) {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++) {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString() {
            var buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < Size; i++) {
                    var b = *(byte*)(thisPtr + i);
                    if (b == 0) {
                        break;
                    }
                    buffer[i] = b;
                    len = i + 1;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, len);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Symbol16 && Equals((Symbol16)obj);
        }

        public override int GetHashCode() {
            fixed (byte* ptr = Bytes)
            {
                unchecked {
                    const int p = 16777619;
                    int hash = (int)2166136261;

                    for (int i = 0; i < Size; i++) {
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

        public static bool operator ==(Symbol16 x, Symbol16 y) {
            return x.Equals(y);
        }
        public static bool operator !=(Symbol16 x, Symbol16 y) {
            return !x.Equals(y);
        }
    }
}
