// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Managed implementation is adapted and optimized from: https://github.com/force-net/Crc32.NET/blob/develop/LICENSE
// The MIT License(MIT)

// Copyright(c) 2016 force

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using static System.Runtime.CompilerServices.Unsafe;
#if HAS_INTRINSICS
using X86 = System.Runtime.Intrinsics.X86;
using Arm = System.Runtime.Intrinsics.Arm;
#endif

namespace Spreads.Algorithms.Hash
{
    public static unsafe class Crc32C
    {
        // See for parallelism:
        // https://stackoverflow.com/questions/17645167/implementing-sse-4-2s-crc32c-in-software/17646775#17646775        
        // and https://github.com/htot/crc32c
        
        private const uint Crc32CPoly = 0x82F63B78u;
        
        private static uint[]? _crc32CTable;
        
        private static uint[] Crc32CTable
        {
            get
            {
                return _crc32CTable ??= Init(Crc32CPoly);

                static uint[] Init(uint poly)
                {
                    var table = new uint[16 * 256];

                    for (uint i = 0; i < 256; i++)
                    {
                        uint res = i;
                        for (int t = 0; t < 16; t++)
                        {
                            for (int k = 0; k < 8; k++) res = (res & 1) == 1 ? poly ^ (res >> 1) : (res >> 1);
                            table[(t * 256) + i] = res;
                        }
                    }
                    return table;
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Append(uint crc, byte* data, int len)
        {
#if HAS_INTRINSICS
            if (X86.Sse42.IsSupported)
            {
                byte* next = data;

                var crc0 = uint.MaxValue ^ crc;

                if (X86.Sse42.X64.IsSupported)
                {
                    while (len >= 8)
                    {
                        crc0 = (uint) X86.Sse42.X64.Crc32(crc0, Read<ulong>(next));
                        next += 8;
                        len -= 8;
                    }
                }

                while (len >= 4)
                {
                    crc0 = X86.Sse42.Crc32(crc0, Read<uint>(next));
                    next += 4;
                    len -= 4;
                }

                if (len >= 2)
                {
                    crc0 = X86.Sse42.Crc32(crc0, Read<ushort>(next));
                    next += 2;
                    len -= 2;
                }

                if (len > 0)
                {
                    crc0 = X86.Sse42.Crc32(crc0, Read<byte>(next));
                }

                return crc0 ^ uint.MaxValue;
            }

            if (Arm.Crc32.IsSupported)
            {
                byte* next = data;

                var crc0 = uint.MaxValue ^ crc;

                if (Arm.Crc32.Arm64.IsSupported)
                {
                    while (len >= 8)
                    {
                        crc0 = Arm.Crc32.Arm64.ComputeCrc32C(crc0, Read<ulong>(next));
                        next += 8;
                        len -= 8;
                    }
                }

                while (len >= 4)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, Read<uint>(next));
                    next += 4;
                    len -= 4;
                }

                if (len >= 2)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, Read<ushort>(next));
                    next += 2;
                    len -= 2;
                }

                if (len > 0)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, Read<byte>(next));
                }

                return crc0 ^ uint.MaxValue;
            }
#endif
            return AppendManaged(crc, data, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint AppendCopy(uint crc, byte* data, int len, byte* copyTarget)
        {
#if HAS_INTRINSICS
            if (X86.Sse42.IsSupported)
            {
                byte* next = data;

                var crc0 = uint.MaxValue ^ crc;

                if (X86.Sse42.X64.IsSupported)
                {
                    while (len >= 8)
                    {
                        crc0 = (uint) X86.Sse42.X64.Crc32(crc0, Read<ulong>(next));

                        CopyBlockUnaligned(copyTarget, next, 8);

                        copyTarget += 8;
                        next += 8;
                        len -= 8;
                    }
                }

                while (len >= 4)
                {
                    crc0 = X86.Sse42.Crc32(crc0, Read<uint>(next));

                    CopyBlockUnaligned(copyTarget, next, 4);

                    copyTarget += 4;
                    next += 4;
                    len -= 4;
                }

                if (len >= 2)
                {
                    crc0 = X86.Sse42.Crc32(crc0, Read<ushort>(next));

                    CopyBlockUnaligned(copyTarget, next, 2);

                    copyTarget += 2;
                    next += 2;
                    len -= 2;
                }

                if (len > 0)
                {
                    crc0 = X86.Sse42.Crc32(crc0, *next);

                    *copyTarget = *next;
                }

                return crc0 ^ uint.MaxValue;
            }

            if (Arm.Crc32.IsSupported)
            {
                byte* next = data;

                var crc0 = uint.MaxValue ^ crc;

                while (len > 0 && ((ulong) next & 7) != 0)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, *next);
                    *copyTarget = *next;
                    next++;
                    copyTarget++;
                    len--;
                }

                if (Arm.Crc32.Arm64.IsSupported)
                {
                    while (len >= 8)
                    {
                        crc0 = Arm.Crc32.Arm64.ComputeCrc32C(crc0, Read<ulong>(next));

                        CopyBlockUnaligned(copyTarget, next, 8);

                        copyTarget += 8;
                        next += 8;
                        len -= 8;
                    }
                }

                while (len >= 4)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, Read<uint>(next));

                    CopyBlockUnaligned(copyTarget, next, 4);

                    copyTarget += 4;
                    next += 4;
                    len -= 4;
                }

                if (len >= 2)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, Read<ushort>(next));

                    CopyBlockUnaligned(copyTarget, next, 2);

                    copyTarget += 2;
                    next += 2;
                    len -= 2;
                }

                if (len > 0)
                {
                    crc0 = Arm.Crc32.ComputeCrc32C(crc0, *next);

                    *copyTarget = *next;
                }

                return crc0 ^ uint.MaxValue;
            }
#endif
            var crc00 = AppendManaged(crc, data, len);
            CopyBlockUnaligned(copyTarget, data, (uint) len);
            return crc00;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint AppendManaged(uint crc, byte* data, int length)
        {
#if DEBUG
            unchecked
            {
#endif
            var table = Crc32CTable;
            uint crcLocal = uint.MaxValue ^ crc;
            int offset = 0;
            while (length >= 16)
            {
                var a = table[(3 * 256) + *(data + offset + 12)]
                        ^ table[(2 * 256) + *(data + offset + 13)]
                        ^ table[(1 * 256) + *(data + offset + 14)]
                        ^ table[(0 * 256) + *(data + offset + 15)];

                var b = table[(7 * 256) + *(data + offset + 8)]
                        ^ table[(6 * 256) + *(data + offset + 9)]
                        ^ table[(5 * 256) + *(data + offset + 10)]
                        ^ table[(4 * 256) + *(data + offset + 11)];

                var c = table[(11 * 256) + *(data + offset + 4)]
                        ^ table[(10 * 256) + *(data + offset + 5)]
                        ^ table[(9 * 256) + *(data + offset + 6)]
                        ^ table[(8 * 256) + *(data + offset + 7)];

                var d = table[(15 * 256) + ((byte) crcLocal ^ *(data + offset))]
                        ^ table[(14 * 256) + ((byte) (crcLocal >> 8) ^ *(data + offset + 1))]
                        ^ table[(13 * 256) + ((byte) (crcLocal >> 16) ^ *(data + offset + 2))]
                        ^ table[(12 * 256) + ((crcLocal >> 24) ^ *(data + offset + 3))];

                crcLocal = d ^ c ^ b ^ a;
                offset += 16;
                length -= 16;
            }

            while (--length >= 0)
                crcLocal = table[(byte) (crcLocal ^ *(data + offset++))] ^ crcLocal >> 8;

            return crcLocal ^ uint.MaxValue;
#if DEBUG
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CalculateCrc32C(byte* data, int len, uint seed = 0)
        {
            return Append(seed, data, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint CalculateCrc32CManaged(byte* data, int len, uint seed = 0)
        {
            return AppendManaged(seed, data, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CopyWithCrc32C(byte* data, int len, byte* copyTarget, uint seed = 0)
        {
            return AppendCopy(seed, data, len, copyTarget);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint CopyWithCrc32CManaged(byte* data, int len, byte* copyTarget, uint seed = 0)
        {
            var crc0 = AppendManaged(seed, data, len);
            CopyBlockUnaligned(copyTarget, data, (uint) len);
            return crc0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint CopyWithCrc32CManual(byte* data, int len, byte* copyTarget, uint seed = 0)
        {
            var crc0 = Append(seed, data, len);
            CopyBlockUnaligned(copyTarget, data, (uint) len);
            return crc0;
        }
    }

    public static class Crc32CExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint Crc32C(in this DirectBuffer buffer, uint seed = 0)
        {
            return Hash.Crc32C.CalculateCrc32C(buffer.Data, buffer.Length, seed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint Crc32C(this Span<byte> span, uint seed = 0)
        {
            fixed (byte* ptr = &span.GetPinnableReference())
            {
                return Hash.Crc32C.CalculateCrc32C(ptr, span.Length, seed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint Crc32C(this ReadOnlySpan<byte> span, uint seed = 0)
        {
            fixed (byte* ptr = &span.GetPinnableReference())
            {
                return Hash.Crc32C.CalculateCrc32C(ptr, span.Length, seed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Crc32C(this Memory<byte> memory, uint seed = 0)
        {
            return memory.Span.Crc32C(seed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Crc32C(this ReadOnlyMemory<byte> memory, uint seed = 0)
        {
            return memory.Span.Crc32C(seed);
        }
    }
}