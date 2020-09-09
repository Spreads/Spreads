using System;
using System.Runtime.CompilerServices;
using Spreads.Buffers;

#if HAS_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace Spreads.Algorithms.Hash.BLAKE2b
{
    public unsafe partial struct Blake2bContext
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void compress(byte* pinput, uint offs, uint cb)
        {
            uint inc = Math.Min(cb, BlockBytes);

            var s = (Blake2bContext*)Unsafe.AsPointer(ref Unsafe.AsRef(this));

            ulong* sh = s->h;
            byte* pin = pinput + offs;
            byte* end = pin + cb;

            do
            {
                t[0] += inc;
                if (t[0] < inc)
                    t[1]++;

                ulong* m = (ulong*)pin;
#if HAS_INTRINSICS
                if (Avx2.IsSupported)
                    mixAvx2(sh, m);
                else
                if (Sse41.IsSupported)
                    mixSse41(sh, m);
                else
#endif
                    mixScalar(sh, m);

                pin += inc;
            } while (pin < end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(DirectBuffer input)
        {
            if (f[0] != 0) ThrowHelper.HashFinalized();

            var s = (Blake2bContext*)Unsafe.AsPointer(ref Unsafe.AsRef(this));

            uint consumed = 0;
            uint remaining = (uint)input.Length;

            uint blockrem = BlockBytes - c;
            if ((c != 0) && (remaining > blockrem))
            {
                if (blockrem != 0)
                    Unsafe.CopyBlockUnaligned(ref b[c], ref *input.Data, blockrem);

                c = 0;
                compress(s->b, 0, BlockBytes);
                consumed += blockrem;
                remaining -= blockrem;
            }

            if (remaining > BlockBytes)
            {
                uint cb = (remaining - 1) & ~((uint)BlockBytes - 1);
                compress(input.Data, consumed, cb);
                consumed += cb;
                remaining -= cb;
            }

            if (remaining != 0)
            {
                Unsafe.CopyBlockUnaligned(ref b[c], ref *(input.Data + consumed), remaining);
                c += remaining;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Hash(Span<byte> hash)
        {
            if (f[0] != 0) ThrowHelper.HashFinalized();

            if (c < BlockBytes)
                Unsafe.InitBlockUnaligned(ref b[c], 0, BlockBytes - c);

            var s = (Blake2bContext*)Unsafe.AsPointer(ref Unsafe.AsRef(this));

            var hSpan = new Span<ulong>(s->h, HashWords);
            Span<ulong> hx = stackalloc ulong[HashWords];
            hSpan.CopyTo(hx);
            var f0 = f[0];
            var f1 = f[1];

            var t0 = t[0];
            var t1 = t[1];

            f[0] = ~0ul;
            compress(s->b, 0, c);

            Unsafe.CopyBlockUnaligned(ref hash[0], ref Unsafe.As<ulong, byte>(ref h[0]), outlen);
            f[0] = f0;
            f[1] = f1;
            t[0] = t0;
            t[1] = t1;
            hx.CopyTo(hSpan);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
         | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public void UpdateHash(DirectBuffer input, Span<byte> hash)
        {
            Update(input);
            Hash(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitUpdateHash(in ReadOnlySpan<byte> input, Span<byte> hash, int digestLength = HashBytes,
            ReadOnlySpan<byte> key = default)
        {
            Init(digestLength, key);
            Update(input);
            Hash(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InitUpdateHash(in DirectBuffer input, Span<byte> hash, int digestLength = HashBytes,
            ReadOnlySpan<byte> key = default)
        {
            Init(digestLength, key);
            Update(input);
            Hash(hash);
        }
    }
}
