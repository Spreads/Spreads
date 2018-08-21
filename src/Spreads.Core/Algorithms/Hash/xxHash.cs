/*
xxHashSharp - A pure C# implementation of xxhash
Copyright (C) 2016 Victor Baybekov
Copyright (C) 2014, Seok-Ju, Yun. (https://github.com/noricube/xxHashSharp)
Original C Implementation Copyright (C) 2012-2014, Yann Collet. (https://code.google.com/p/xxhash/)
BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Spreads.Utils;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Algorithms.Hash
{
    public static unsafe class XxHash
    {
        public struct XxHashState32
        {
            internal ulong TotalLen;
            internal uint Seed;
            internal uint V1;
            internal uint V2;
            internal uint V3;
            internal uint V4;
            internal fixed byte Memory[16];
            internal uint Memsize;

            public XxHashState32(uint seed = 0)
            {
                unchecked
                {
                    Seed = seed;
                    V1 = seed + PRIME32_1 + PRIME32_2;
                    V2 = seed + PRIME32_2;
                    V3 = seed + 0;
                    V4 = seed - PRIME32_1;
                    TotalLen = 0;
                    Memsize = 0;
                }
            }
        };

        // ReSharper disable InconsistentNaming
        private const uint PRIME32_1 = 2654435761U;
        private const uint PRIME32_2 = 2246822519U;
        private const uint PRIME32_3 = 3266489917U;
        private const uint PRIME32_4 = 668265263U;
        private const uint PRIME32_5 = 374761393U;
        // ReSharper restore InconsistentNaming

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CalculateHash(IntPtr data, int len, uint seed = 0)
        {
#if DEBUG
            unchecked
            {
#endif
            uint h32;
            int index = 0;

            if (len >= 16)
            {
                int limit = len - 16;
                uint v1 = seed + PRIME32_1 + PRIME32_2;
                uint v2 = seed + PRIME32_2;
                uint v3 = seed + 0;
                uint v4 = seed - PRIME32_1;

                do
                {
                    v1 = CalcSubHash(v1, data, index);
                    index += 4;
                    v2 = CalcSubHash(v2, data, index);
                    index += 4;
                    v3 = CalcSubHash(v3, data, index);
                    index += 4;
                    v4 = CalcSubHash(v4, data, index);
                    index += 4;
                } while (index <= limit);

                h32 = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
            }
            else
            {
                h32 = seed + PRIME32_5;
            }

            h32 += (uint)len;

            while (index <= len - 4)
            {
                h32 += *(uint*)(data + index) * PRIME32_3;
                h32 = RotateLeft(h32, 17) * PRIME32_4;
                index += 4;
            }

            while (index < len)
            {
                h32 += *(byte*)(data + index) * PRIME32_5;
                h32 = RotateLeft(h32, 11) * PRIME32_1;
                index++;
            }

            h32 ^= h32 >> 15;
            h32 *= PRIME32_2;
            h32 ^= h32 >> 13;
            h32 *= PRIME32_3;
            h32 ^= h32 >> 16;

            return h32;
#if DEBUG
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Update(ref XxHashState32 state, IntPtr input, int len)
        {
#if DEBUG
            unchecked
            {
#endif
            int index = 0;

            state.TotalLen += (uint)len;

            if (state.Memsize + len < 16)
            {
                fixed (byte* ptr = state.Memory)
                {
                    Unsafe.CopyBlockUnaligned(ptr + state.Memsize, (byte*)input, (uint)len);
                }

                //Array.Copy(input, 0, state.Memory, state.Memsize, len);
                state.Memsize += (uint)len;

                return true;
            }

            if (state.Memsize > 0)
            {
                fixed (byte* ptr = state.Memory)
                {
                    Unsafe.CopyBlockUnaligned(ptr + state.Memsize, (byte*)input, (uint)(16 - state.Memsize));
                }

                //Array.Copy(input, 0, state.Memory, state.Memsize, 16 - state.Memsize);
                fixed (byte* ptr = state.Memory)
                {
                    state.V1 = CalcSubHash(state.V1, (IntPtr)ptr, index);
                    index += 4;
                    state.V2 = CalcSubHash(state.V2, (IntPtr)ptr, index);
                    index += 4;
                    state.V3 = CalcSubHash(state.V3, (IntPtr)ptr, index);
                    index += 4;
                    state.V4 = CalcSubHash(state.V4, (IntPtr)ptr, index);
                    //index += 4;
                }

                index = 0;
                state.Memsize = 0;
            }

            if (index <= len - 16)
            {
                int limit = len - 16;
                uint v1 = state.V1;
                uint v2 = state.V2;
                uint v3 = state.V3;
                uint v4 = state.V4;

                do
                {
                    v1 = CalcSubHash(v1, input, index);
                    index += 4;
                    v2 = CalcSubHash(v2, input, index);
                    index += 4;
                    v3 = CalcSubHash(v3, input, index);
                    index += 4;
                    v4 = CalcSubHash(v4, input, index);
                    index += 4;
                } while (index <= limit);

                state.V1 = v1;
                state.V2 = v2;
                state.V3 = v3;
                state.V4 = v4;
            }

            if (index < len)
            {
                fixed (byte* ptr = state.Memory)
                {
                    Unsafe.CopyBlockUnaligned(ptr + 0, (byte*)(input + index), (uint)(len - index));
                }

                //Array.Copy(input, index, state.Memory, 0, len - index);
                state.Memsize = (uint)(len - index);
            }

            return true;
#if DEBUG
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Digest(XxHashState32 state)
        {
#if DEBUG
            unchecked
            {
#endif
            uint h32;
            int index = 0;
            if (state.TotalLen >= 16)
            {
                h32 = RotateLeft(state.V1, 1) + RotateLeft(state.V2, 7) + RotateLeft(state.V3, 12) +
                      RotateLeft(state.V4, 18);
            }
            else
            {
                h32 = state.Seed + PRIME32_5;
            }

            h32 += (uint)state.TotalLen;

            while (index <= state.Memsize - 4)
            {
                h32 += *(uint*)(state.Memory + index) * PRIME32_3;
                h32 = RotateLeft(h32, 17) * PRIME32_4;
                index += 4;
            }

            while (index < state.Memsize)
            {
                h32 += state.Memory[index] * PRIME32_5;
                h32 = RotateLeft(h32, 11) * PRIME32_1;
                index++;
            }

            h32 ^= h32 >> 15;
            h32 *= PRIME32_2;
            h32 ^= h32 >> 13;
            h32 *= PRIME32_3;
            h32 ^= h32 >> 16;

            return h32;
#if DEBUG
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint CalcSubHash(uint value, IntPtr buf, int index)
        {
#if DEBUG
            unchecked
            {
#endif
            var readValue = *(uint*)(buf + index);
            value += readValue * PRIME32_2;
            value = RotateLeft(value, 13);
            value *= PRIME32_1;
            return value;
#if DEBUG
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int count)
        {
#if DEBUG
            unchecked
            {
#endif
            return (value << count) | (value >> (32 - count));
#if DEBUG
            }
#endif
        }
    }
}
