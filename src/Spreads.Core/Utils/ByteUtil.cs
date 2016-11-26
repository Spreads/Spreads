// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Utils {

    /// <summary>
    /// Utility to copy blocks of memory
    /// </summary>
    public unsafe class ByteUtil {
        private static readonly int _wordSize = IntPtr.Size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryCopy(byte* destination, byte* source, uint length) {
            // NB The behavior of cpblk is unspecified if the source and destination areas overlap.
            // https://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes.cpblk%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
            Unsafe.CopyBlockUnaligned(destination, source, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryMove(byte* destination, byte* source, uint length) {
            if (destination < source) {
                var pos = 0;
                int nextPos;
                if (_wordSize == 8) {
                    nextPos = pos + 8;
                    while (nextPos <= length) {
                        *(long*)(destination + pos) = *(long*)(source + pos);
                        pos = nextPos;
                        nextPos += 8;
                    }
                } else {
                    nextPos = pos + 4;
                    while (nextPos <= length) {
                        *(int*)(destination + pos) = *(int*)(source + pos);
                        pos = nextPos;
                        nextPos += 4;
                    }
                }
                while (pos < length) {
                    *(byte*)(destination + pos) = *(byte*)(source + pos);
                    pos++;
                }
            } else if (destination > source) {
                var pos = (int)length;
                int nextPos;
                if (_wordSize == 8) {
                    nextPos = pos - 8;
                    while (nextPos >= 0) {
                        *(long*)(destination + pos) = *(long*)(source + pos);
                        pos = nextPos;
                        nextPos -= 8;
                    }
                } else {
                    nextPos = pos - 4;
                    while (nextPos >= 0) {
                        *(int*)(destination + pos) = *(int*)(source + pos);
                        pos = nextPos;
                        nextPos -= 4;
                    }
                }
                while (pos > 0) {
                    *(byte*)(destination + pos) = *(byte*)(source + pos);
                    pos--;
                }
            }
        }

        public static int GetHashCode(IntPtr ptr, int len) {
            unchecked {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < len; i++)
                    hash = (hash ^ *(byte*)(ptr + i)) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
    }
}
