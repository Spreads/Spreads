using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads {
    /// <summary>
    /// Utility to copy blocks of memory
    /// </summary>
    public unsafe class ByteUtil {
        //[StructLayout(LayoutKind.Sequential, Pack = 64, Size = 64)]
        //internal struct CopyChunk64 {
        //    private fixed byte _bytes[64];
        //}

        private static readonly int _wordSize = IntPtr.Size;

        [StructLayout(LayoutKind.Sequential, Pack = 32, Size = 32)]
        internal struct CopyChunk32 {
            private readonly long _l1;
            private readonly long _l2;
            private readonly long _l3;
            private readonly long _l4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryCopy(byte* destination, byte* source, uint length) {
            var pos = 0;
            int nextPos;
            nextPos = pos + 32;
            while (nextPos <= length) {
                *(CopyChunk32*)(destination + pos) = *(CopyChunk32*)(source + pos);
                pos = nextPos;
                nextPos += 32;
            }
            nextPos = pos + 8;
            while (nextPos <= length) {
                *(long*)(destination + pos) = *(long*)(source + pos);
                pos = nextPos;
                nextPos += 8;
            }

            while (pos < length) {
                *(byte*)(destination + pos) = *(byte*)(source + pos);
                pos++;
            }
        }

        // TODO Check if (CopyChunk32*) is atomic (repost deleted SO question if needed)
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
