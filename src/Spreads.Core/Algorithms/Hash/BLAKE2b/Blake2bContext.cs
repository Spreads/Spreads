// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Implementation is refactored and optimized from: Blake2Fast https://github.com/saucecontrol/Blake2Fast/
// Copyright (c) 2018 Clinton Ingram
// The MIT License

using System;
using System.Runtime.CompilerServices;

#if NETCOREAPP2_1
using System.Runtime.Intrinsics.X86;
#endif

using System.Runtime.InteropServices;
using Spreads.Buffers;

namespace Spreads.Algorithms.Hash
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe partial struct Blake2bContext
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Blake2bContextFinalizable
        {
            public fixed ulong h[HashWords];
            public fixed ulong t[2];
            public fixed ulong f[2];
        }

        public const int WordSize = sizeof(ulong);
        public const int BlockWords = 16;
        public const int BlockBytes = BlockWords * WordSize;
        public const int HashWords = 8;
        public const int HashBytes = HashWords * WordSize;
        public const int MaxKeyBytes = HashBytes;

        private static readonly ulong[] iv = new[] {
            0x6A09E667F3BCC908ul, 0xBB67AE8584CAA73Bul,
            0x3C6EF372FE94F82Bul, 0xA54FF53A5F1D36F1ul,
            0x510E527FADE682D1ul, 0x9B05688C2B3E6C1Ful,
            0x1F83D9ABFB41BD6Bul, 0x5BE0CD19137E2179ul
        };

        private Blake2bContextFinalizable htf;

        private fixed byte b[BlockBytes];

#if NETCOREAPP2_1
        private fixed ulong viv[HashWords];
        private fixed byte vrm[32];
#endif

        private uint c;
        private uint outlen;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void addLength(uint len)
        {
            htf.t[0] += len;
            if (htf.t[0] < len)
                htf.t[1]++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe private static void compress(Blake2bContext* s, byte* input)
        {
            ulong* m = (ulong*)input;

#if NETCOREAPP2_1
            if (Sse41.IsSupported)
            {
                mixSse41(s, m);
            }
            else
#endif
            {
                mixScalar(s, m);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Init(int digestLength = HashBytes, ReadOnlySpan<byte> key = default)
        {
            if (digestLength == 0 || (uint)digestLength > HashBytes)
            {
                ThrowBadDigestLength();
            }

            uint keylen = (uint)key.Length;

            if (keylen > MaxKeyBytes)
            {
                ThrowBadKeyLength();
            }

            outlen = (uint)digestLength;
            Unsafe.CopyBlock(ref Unsafe.As<ulong, byte>(ref htf.h[0]), ref Unsafe.As<ulong, byte>(ref iv[0]), HashBytes);
            htf.h[0] ^= 0x01010000u ^ (keylen << 8) ^ outlen;

#if NETCOREAPP2_1
            if (Sse41.IsSupported)
            {
                Unsafe.CopyBlock(ref Unsafe.As<ulong, byte>(ref viv[0]), ref Unsafe.As<ulong, byte>(ref iv[0]), HashBytes);
                Unsafe.CopyBlock(ref vrm[0], ref rormask[0], 32);
            }
#endif
            if (keylen > 0)
            {
                Unsafe.CopyBlock(ref b[0], ref MemoryMarshal.GetReference(key), keylen);
                c = BlockBytes;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadDigestLength()
        {
            throw new ArgumentOutOfRangeException("digestLength", $"Value must be between 1 and {HashBytes}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadKeyLength()
        {
            throw new ArgumentException($"Key must be between 0 and {MaxKeyBytes} bytes in length", "key");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(in DirectBuffer input)
        {
            uint inlen = (uint)input.Length;

            uint clen = 0u;
            uint blockrem = BlockBytes - c;

            var s = (Blake2bContext*)Unsafe.AsPointer(ref Unsafe.AsRef(this));

            if ((c > 0u) && (inlen > blockrem))
            {
                if (blockrem > 0)
                {
                    Unsafe.CopyBlockUnaligned(s->b + c, input._pointer, blockrem);
                }
                addLength(BlockBytes);

                compress(s, s->b);

                clen += blockrem;
                inlen -= blockrem;
                c = 0u;
            }

            if (inlen + clen > BlockBytes)
            {
                byte* pinput = input._pointer;

                while (inlen > BlockBytes)
                {
                    addLength(BlockBytes);
                    compress(s, pinput + clen);

                    clen += BlockBytes;
                    inlen -= BlockBytes;
                }

                c = 0u;
            }

            if (inlen > 0)
            {
                Unsafe.CopyBlockUnaligned(s->b + c, (input._pointer + clen), inlen);

                c += inlen;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void finish(Span<byte> hash)
        {
            if (htf.f[0] != 0)
            { ThrowHashAlreadyFinalized(); }

            if (c < BlockBytes)
            { Unsafe.InitBlockUnaligned(ref b[c], 0, BlockBytes - c); }

            addLength(c);
            htf.f[0] = unchecked((ulong)~0);
            var s = (Blake2bContext*)Unsafe.AsPointer(ref Unsafe.AsRef(this));
            {
                compress(s, s->b);
            }

            Unsafe.CopyBlock(ref hash[0], ref Unsafe.As<ulong, byte>(ref htf.h[0]), outlen);
        }

        private static void ThrowHashAlreadyFinalized()
        {
            throw new InvalidOperationException("Hash has already been finalized.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void finish(ref Blake2bContext ctx1, Span<byte> hash)
        {
            var prevHtf = ctx1.htf;
            // var ctx1 = ctx;
            if (ctx1.c < BlockBytes)
                Unsafe.InitBlockUnaligned(ref ctx1.b[ctx1.c], 0, BlockBytes - ctx1.c);

            ctx1.addLength(ctx1.c);
            ctx1.htf.f[0] = unchecked((ulong)~0);
            var s = (Blake2bContext*)Unsafe.AsPointer(ref Unsafe.AsRef(ctx1));
            compress(s, s->b);

            Unsafe.CopyBlock(ref hash[0], ref Unsafe.As<ulong, byte>(ref ctx1.htf.h[0]), ctx1.outlen);
            ctx1.htf = prevHtf;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFinish(Span<byte> output, out int bytesWritten)
        {
            if (output.Length < outlen)
            {
                bytesWritten = 0;
                return false;
            }

            finish(output);
            bytesWritten = (int)outlen;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFinish(ref Blake2bContext ctx, Span<byte> output, out int bytesWritten)
        {
            if (output.Length < ctx.outlen)
            {
                bytesWritten = 0;
                return false;
            }

            finish(ref ctx, output);
            bytesWritten = (int)ctx.outlen;
            return true;
        }
    }
}
