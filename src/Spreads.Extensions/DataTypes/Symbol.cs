using System;
using System.Diagnostics;
using System.Text;
using Spreads.Serialization;

namespace Spreads.DataTypes {

    [DebuggerDisplay("{AsString}")]
    public unsafe struct Symbol : IEquatable<Symbol> {
        private fixed byte Bytes[16];

        public Symbol(string symbol) {
            var byteCount = Encoding.UTF8.GetByteCount(symbol);
            if (byteCount > 16) {
                throw new ArgumentOutOfRangeException("Symbol length is too large");
            }
            fixed (char* charPtr = symbol)
            fixed (byte* ptr = Bytes)
            {
                Encoding.UTF8.GetBytes(charPtr, symbol.Length, (byte*)ptr, 16);
            }
        }

        public string AsString => ToString();

        public bool Equals(Symbol other) {
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < 16; i++) {
                    if (*(byte*)(thisPtr + i) != *(byte*)(other.Bytes + i)) return false;
                }
            }
            return true;
        }

        public override string ToString() {
            var buffer = BinaryConvertorExtensions.ThreadStaticBuffer;
            var len = 0;
            fixed (byte* thisPtr = Bytes)
            {
                for (int i = 0; i < 16; i++) {
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

        public static bool operator ==(Symbol x, Symbol y) {
            return x.Equals(y);
        }
        public static bool operator !=(Symbol x, Symbol y) {
            return !x.Equals(y);
        }
    }
}
