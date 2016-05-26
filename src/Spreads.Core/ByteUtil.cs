using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {
    /// <summary>
    /// Utility to copy blocks of memory
    /// </summary>
    public unsafe class ByteUtil {
        [StructLayout(LayoutKind.Sequential, Pack = 64, Size = 64)]
        internal struct CopyChunk64 {
            private fixed byte _bytes[64];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 32, Size = 32)]
        internal struct CopyChunk32 {
            private fixed byte _bytes[32];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 16)]
        internal struct CopyChunk16 {
            private fixed byte _bytes[16];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryCopy(IntPtr destination, IntPtr source, uint length) {
            int pos = 0;
            while (pos < length) {
                int remaining = (int)length - pos;
                if (remaining >= 64) {
                    *(CopyChunk64*)(destination + pos) = *(CopyChunk64*)(source + pos);
                    pos += 64;
                    continue;
                }
                if (remaining >= 32) {
                    *(CopyChunk32*)(destination + pos) = *(CopyChunk32*)(source + pos);
                    pos += 32;
                    continue;
                }
                if (remaining >= 16) {
                    *(CopyChunk16*)(destination + pos) = *(CopyChunk16*)(source + pos);
                    pos += 16;
                    continue;
                }
                if (remaining >= 8) {
                    *(long*)(destination + pos) = *(long*)(source + pos);
                    pos += 8;
                    continue;
                }
                if (remaining >= 4) {
                    *(int*)(destination + pos) = *(int*)(source + pos);
                    pos += 4;
                    continue;
                }
                if (remaining >= 1) {
                    *(byte*)(destination + pos) = *(byte*)(source + pos);
                    pos++;
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
