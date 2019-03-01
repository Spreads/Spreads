// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Implementation is refactored and optimized from: Blake2Fast https://github.com/saucecontrol/Blake2Fast/
// Copyright (c) 2018 Clinton Ingram
// The MIT License

#if NETCOREAPP3_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Spreads.Algorithms.Hash
{
    public unsafe partial struct Blake2bContext
	{
		private static readonly byte[] rormask = new byte[] {
			2, 3, 4, 5, 6, 7, 0, 1, 10, 11, 12, 13, 14, 15, 8, 9, //r16
			3, 4, 5, 6, 7, 0, 1, 2, 11, 12, 13, 14, 15, 8, 9, 10  //r24
		};

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<ulong> ror64_32(ref Vector128<ulong> x) => Sse2.Shuffle(x.AsUInt32(), 0b_10_11_00_01).AsUInt64();

	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector256<ulong> ror64_32_avx(ref Vector256<ulong> x) => Avx2.Shuffle(x.AsUInt32(), 0b_10_11_00_01).AsUInt64();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<ulong> ror64_63(ref Vector128<ulong> x) => Sse2.Xor(Sse2.ShiftRightLogical(x, 63), Sse2.Add(x, x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<ulong> ror64_shuffle(ref Vector128<ulong> x, ref Vector128<sbyte> y) =>
			Ssse3.Shuffle(x.AsSByte(), y).AsUInt64();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<ulong> blend_ulong(ref Vector128<ulong> x, ref Vector128<ulong> y, byte m) =>
			Sse41.Blend(x.AsUInt16(), y.AsUInt16(), m).AsUInt64();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<ulong> alignr_ulong(ref Vector128<ulong> x, ref Vector128<ulong> y, byte m) =>
			Ssse3.AlignRight(x.AsSByte(), y.AsSByte(), m).AsUInt64();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Vector128<ulong> shuffle_ulong(ref Vector128<ulong> x, byte m)
        {
            var y = x.AsUInt32();
            var z = Sse2.Shuffle(y, m);
            return z.AsUInt64();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void g1(ref Vector128<ulong> row1l, ref Vector128<ulong> row2l, ref Vector128<ulong> row3l, ref Vector128<ulong> row4l,
			ref Vector128<ulong> row1h, ref Vector128<ulong> row2h, ref Vector128<ulong> row3h, ref Vector128<ulong> row4h, ref Vector128<ulong> b0, ref Vector128<ulong> b1, ref Vector128<sbyte> r24)
		{
            row1l = Sse2.Add(Sse2.Add(row1l, b0), row2l);
			row1h = Sse2.Add(Sse2.Add(row1h, b1), row2h);

			row4l = Sse2.Xor(row4l, row1l);
			row4h = Sse2.Xor(row4h, row1h);
			row4l = ror64_32(ref row4l);
			row4h = ror64_32(ref row4h);

			row3l = Sse2.Add(row3l, row4l);
			row3h = Sse2.Add(row3h, row4h);

			row2l = Sse2.Xor(row2l, row3l);
			row2h = Sse2.Xor(row2h, row3h);
			row2l = ror64_shuffle(ref row2l, ref r24);
			row2h = ror64_shuffle(ref row2h, ref r24);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void g2(ref Vector128<ulong> row1l, ref Vector128<ulong> row2l, ref Vector128<ulong> row3l, ref Vector128<ulong> row4l,
			ref Vector128<ulong> row1h, ref Vector128<ulong> row2h, ref Vector128<ulong> row3h, ref Vector128<ulong> row4h, ref Vector128<ulong> b0, ref Vector128<ulong> b1, ref Vector128<sbyte> r16)
		{
			row1l = Sse2.Add(Sse2.Add(row1l, b0), row2l);
			row1h = Sse2.Add(Sse2.Add(row1h, b1), row2h);

			row4l = Sse2.Xor(row4l, row1l);
			row4h = Sse2.Xor(row4h, row1h);
			row4l = ror64_shuffle(ref row4l, ref r16);
			row4h = ror64_shuffle(ref row4h, ref r16);

			row3l = Sse2.Add(row3l, row4l);
			row3h = Sse2.Add(row3h, row4h);

			row2l = Sse2.Xor(row2l, row3l);
			row2h = Sse2.Xor(row2h, row3h);
			row2l = ror64_63(ref row2l);
			row2h = ror64_63(ref row2h);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void diagonalize(ref Vector128<ulong> row1l, ref Vector128<ulong> row2l, ref Vector128<ulong> row3l, ref Vector128<ulong> row4l,
			ref Vector128<ulong> row1h, ref Vector128<ulong> row2h, ref Vector128<ulong> row3h, ref Vector128<ulong> row4h, ref Vector128<ulong> b0)
		{
			var t0 = Ssse3.AlignRight(row2h.AsSByte(), row2l.AsSByte(), 8);
			var t1 = Ssse3.AlignRight(row2l.AsSByte(), row2h.AsSByte(), 8);
			row2l = t0.AsUInt64();
			row2h = t1.AsUInt64();

			b0 = row3l;
			row3l = row3h;
			row3h = b0;

			t0 = Ssse3.AlignRight(row4h.AsSByte(), row4l.AsSByte(), 8);
			t1 = Ssse3.AlignRight(row4l.AsSByte(), row4h.AsSByte(), 8);
			row4l = t1.AsUInt64();
			row4h = t0.AsUInt64();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void undiagonalize(ref Vector128<ulong> row1l, ref Vector128<ulong> row2l, ref Vector128<ulong> row3l, ref Vector128<ulong> row4l,
			ref Vector128<ulong> row1h, ref Vector128<ulong> row2h, ref Vector128<ulong> row3h, ref Vector128<ulong> row4h, ref Vector128<ulong> b0)
		{
			var t0 = Ssse3.AlignRight(row2l.AsSByte(), row2h.AsSByte(), 8);
			var t1 = Ssse3.AlignRight(row2h.AsSByte(), row2l.AsSByte(), 8);
			row2l = t0.AsUInt64();
			row2h = t1.AsUInt64();

			b0 = row3l;
			row3l = row3h;
			row3h = b0;

			t0 = Ssse3.AlignRight(row4l.AsSByte(), row4h.AsSByte(), 8);
			t1 = Ssse3.AlignRight(row4h.AsSByte(), row4l.AsSByte(), 8);
			row4l = t1.AsUInt64();
			row4h = t0.AsUInt64();
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe private static void mixSse41(Blake2bContext* s, ulong* m)
        {
            var hptr = s->htf.h;
            var row1l = Unsafe.As<ulong, Vector128<ulong>>(ref hptr[0]);
			var row1h = Unsafe.As<ulong, Vector128<ulong>>(ref hptr[2]);
			var row2l = Unsafe.As<ulong, Vector128<ulong>>(ref hptr[4]);
			var row2h = Unsafe.As<ulong, Vector128<ulong>>(ref hptr[6]);

            var row3l = Unsafe.As<ulong, Vector128<ulong>>(ref V.iv[0]);
			var row3h = Unsafe.As<ulong, Vector128<ulong>>(ref V.iv[2]);
			var row4l = Unsafe.As<ulong, Vector128<ulong>>(ref V.iv[4]);
			var row4h = Unsafe.As<ulong, Vector128<ulong>>(ref V.iv[6]);

			row4l = Sse2.Xor(row4l, Sse2.LoadVector128(s->htf.t));
			row4h = Sse2.Xor(row4h, Sse2.LoadVector128(s->htf.f));

			//ROUND 1
			var m0 = Sse2.LoadVector128(m);
			var m1 = Sse2.LoadVector128(m + 2);
			var m2 = Sse2.LoadVector128(m + 4);
			var m3 = Sse2.LoadVector128(m + 6);

			var b0 = Sse2.UnpackLow(m0, m1);
			var b1 = Sse2.UnpackLow(m2, m3);

			var r16 = Sse2.LoadVector128((sbyte*)V.rm);
			var r24 = Sse2.LoadVector128((sbyte*)V.rm + 16);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m0, m1);
			b1 = Sse2.UnpackHigh(m2, m3);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			var m4 = Sse2.LoadVector128(m + 8);
			var m5 = Sse2.LoadVector128(m + 10);
			var m6 = Sse2.LoadVector128(m + 12);
			var m7 = Sse2.LoadVector128(m + 14);

			b0 = Sse2.UnpackLow(m4, m5);
			b1 = Sse2.UnpackLow(m6, m7);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m4, m5);
			b1 = Sse2.UnpackHigh(m6, m7);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 2
			b0 = Sse2.UnpackLow(m7, m2);
			b1 = Sse2.UnpackHigh(m4, m6);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m5, m4);
			b1 = alignr_ulong(ref m3, ref m7, 8);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = shuffle_ulong(ref m0, 0b_01_00_11_10);
			b1 = Sse2.UnpackHigh(m5, m2);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m6, m1);
			b1 = Sse2.UnpackHigh(m3, m1);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 3
			b0 = alignr_ulong(ref m6, ref m5, 8);
			b1 = Sse2.UnpackHigh(m2, m7);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m4, m0);
			b1 = blend_ulong(ref m1, ref m6, 0b_1111_0000);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = blend_ulong(ref m5, ref m1, 0b_1111_0000);
			b1 = Sse2.UnpackHigh(m3, m4);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m7, m3);
			b1 = alignr_ulong(ref m2, ref m0, 8);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 4
			b0 = Sse2.UnpackHigh(m3, m1);
			b1 = Sse2.UnpackHigh(m6, m5);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m4, m0);
			b1 = Sse2.UnpackLow(m6, m7);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = blend_ulong(ref m1, ref m2, 0b_1111_0000);
			b1 = blend_ulong(ref m2, ref m7, 0b_1111_0000);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m3, m5);
			b1 = Sse2.UnpackLow(m0, m4);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 5
			b0 = Sse2.UnpackHigh(m4, m2);
			b1 = Sse2.UnpackLow(m1, m5);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = blend_ulong(ref m0, ref m3, 0b_1111_0000);
			b1 = blend_ulong(ref m2, ref m7, 0b_1111_0000);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = blend_ulong(ref m7, ref m5, 0b_1111_0000);
			b1 = blend_ulong(ref m3, ref m1, 0b_1111_0000);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = alignr_ulong(ref m6, ref m0, 8);
			b1 = blend_ulong(ref m4, ref m6, 0b_1111_0000);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 6
			b0 = Sse2.UnpackLow(m1, m3);
			b1 = Sse2.UnpackLow(m0, m4);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m6, m5);
			b1 = Sse2.UnpackHigh(m5, m1);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = blend_ulong(ref m2, ref m3, 0b_1111_0000);
			b1 = Sse2.UnpackHigh(m7, m0);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m6, m2);
			b1 = blend_ulong(ref m7, ref m4, 0b_1111_0000);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 7
			b0 = blend_ulong(ref m6, ref m0, 0b_1111_0000);
			b1 = Sse2.UnpackLow(m7, m2);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m2, m7);
			b1 = alignr_ulong(ref m5, ref m6, 8);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = Sse2.UnpackLow(m0, m3);
			b1 = shuffle_ulong(ref m4, 0b_01_00_11_10);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m3, m1);
			b1 = blend_ulong(ref m1, ref m5, 0b_1111_0000);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 8
			b0 = Sse2.UnpackHigh(m6, m3);
			b1 = blend_ulong(ref m6, ref m1, 0b_1111_0000);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = alignr_ulong(ref m7, ref m5, 8);
			b1 = Sse2.UnpackHigh(m0, m4);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = Sse2.UnpackHigh(m2, m7);
			b1 = Sse2.UnpackLow(m4, m1);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m0, m2);
			b1 = Sse2.UnpackLow(m3, m5);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 9
			b0 = Sse2.UnpackLow(m3, m7);
			b1 = alignr_ulong(ref m0, ref m5, 8);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m7, m4);
			b1 = alignr_ulong(ref m4, ref m1, 8);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = m6;
			b1 = alignr_ulong(ref m5, ref m0, 8);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = blend_ulong(ref m1, ref m3, 0b_1111_0000);
			b1 = m2;

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 10
			b0 = Sse2.UnpackLow(m5, m4);
			b1 = Sse2.UnpackHigh(m3, m0);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m1, m2);
			b1 = blend_ulong(ref m3, ref m2, 0b_1111_0000);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = Sse2.UnpackHigh(m7, m4);
			b1 = Sse2.UnpackHigh(m1, m6);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = alignr_ulong(ref m7, ref m5, 8);
			b1 = Sse2.UnpackLow(m6, m0);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 11
			b0 = Sse2.UnpackLow(m0, m1);
			b1 = Sse2.UnpackLow(m2, m3);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m0, m1);
			b1 = Sse2.UnpackHigh(m2, m3);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = Sse2.UnpackLow(m4, m5);
			b1 = Sse2.UnpackLow(m6, m7);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackHigh(m4, m5);
			b1 = Sse2.UnpackHigh(m6, m7);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			//ROUND 12
			b0 = Sse2.UnpackLow(m7, m2);
			b1 = Sse2.UnpackHigh(m4, m6);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m5, m4);
			b1 = alignr_ulong(ref m3, ref m7, 8);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			diagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			b0 = shuffle_ulong(ref m0, 0b_01_00_11_10);
			b1 = Sse2.UnpackHigh(m5, m2);

			g1(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r24);

			b0 = Sse2.UnpackLow(m6, m1);
			b1 = Sse2.UnpackHigh(m3, m1);

			g2(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0, ref b1, ref r16);
			undiagonalize(ref row1l, ref row2l, ref row3l, ref row4l, ref row1h, ref row2h, ref row3h, ref row4h, ref b0);

			row1l = Sse2.Xor(row1l, row3l);
			row1h = Sse2.Xor(row1h, row3h);
			row1l = Sse2.Xor(row1l, Sse2.LoadVector128(hptr));
			row1h = Sse2.Xor(row1h, Sse2.LoadVector128(hptr + 2));
			Sse2.Store(hptr, row1l);
			Sse2.Store(hptr + 2, row1h);

			row2l = Sse2.Xor(row2l, row4l);
			row2h = Sse2.Xor(row2h, row4h);
			row2l = Sse2.Xor(row2l, Sse2.LoadVector128(hptr + 4));
			row2h = Sse2.Xor(row2h, Sse2.LoadVector128(hptr + 6));
			Sse2.Store(hptr + 4, row2l);
			Sse2.Store(hptr + 6, row2h);
		}
	}
}
#endif
