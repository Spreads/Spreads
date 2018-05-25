// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Vectorized copy function is modified from https://github.com/IllyriadGames/ByteArrayExtensions
// Copyright 2015 Illyriad Games Ltd, Ben Adams, Licenced as Apache 2.0


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;
// ReSharper disable InconsistentNaming

namespace Spreads.Utils
{
    internal static unsafe class VectorByteExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo(this Vector<byte> vector, void* dst)
        {
            Unsafe.Write(dst, vector);
        }
    }

    /// <summary>
    /// Utility to copy blocks of memory
    /// </summary>
    public unsafe class ByteUtil
    {
        private static readonly int _wordSize = IntPtr.Size;

        // Will be Jit'd to consts https://github.com/dotnet/coreclr/issues/1079
        private static readonly int _vectorSpan = Vector<byte>.Count;
        private static readonly int _vectorSpan2 = Vector<byte>.Count * 2;
        private static readonly int _vectorSpan3 = Vector<byte>.Count * 3;
        private static readonly int _vectorSpan4 = Vector<byte>.Count * 4;

        private const int _longSpan = sizeof(long);
        private const int _longSpan2 = sizeof(long) * 2;
        private const int _longSpan3 = sizeof(long) * 3;
        private const int _intSpan = sizeof(int);


        // TODO Vectorized Clear for pools

        // TODO try generic vectorized copy with typeof(T)==... check
        // for the hottest types explicit, for blittable type could pin manually


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorizedCopy(byte[] src, long srcOffset, byte[] dst, long dstOffset, long length)
        {
#if NET451
            if ((ulong)srcOffset + (ulong)length > (ulong)(src).LongLength)
#else
            if ((ulong)srcOffset + (ulong)length > (ulong)src.Length)
#endif
            {
                ThrowHelper.ThrowArgumentException("Not enough space in src");
            }

#if NET451
            if ((ulong)dstOffset + (ulong)length > (ulong)(dst).LongLength)
#else
            if ((ulong)dstOffset + (ulong)length > (ulong)dst.Length)
#endif
            {
                ThrowHelper.ThrowArgumentException("Not enough space in dst");
            }

            var srcPtr = Unsafe.AsPointer(ref Unsafe.AddByteOffset(ref src[0], (IntPtr)srcOffset));
            var dstPtr = Unsafe.AsPointer(ref Unsafe.AddByteOffset(ref dst[0], (IntPtr)dstOffset));
            VectorizedCopy((byte*)dstPtr, (byte*)srcPtr, (uint)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorizedCopy(byte* dst, byte* src, ulong length)
        {
            var count = (int)length;
            var srcOffset = 0;
            var dstOffset = 0;

            while (count >= _vectorSpan4)
            {
                ReadUnaligned<Vector<byte>>(src + srcOffset).CopyTo(dst + dstOffset);
                ReadUnaligned<Vector<byte>>(src + srcOffset + _vectorSpan).CopyTo(dst + dstOffset + _vectorSpan);
                ReadUnaligned<Vector<byte>>(src + srcOffset + _vectorSpan2).CopyTo(dst + dstOffset + _vectorSpan2);
                ReadUnaligned<Vector<byte>>(src + srcOffset + _vectorSpan3).CopyTo(dst + dstOffset + _vectorSpan3);
                if (count == _vectorSpan4) return;
                count -= _vectorSpan4;
                srcOffset += _vectorSpan4;
                dstOffset += _vectorSpan4;
            }
            if (count >= _vectorSpan2)
            {
                ReadUnaligned<Vector<byte>>(src + srcOffset).CopyTo(dst + dstOffset);
                ReadUnaligned<Vector<byte>>(src + srcOffset + _vectorSpan).CopyTo(dst + dstOffset + _vectorSpan);
                if (count == _vectorSpan2) return;
                count -= _vectorSpan2;
                srcOffset += _vectorSpan2;
                dstOffset += _vectorSpan2;
            }
            if (count >= _vectorSpan)
            {
                ReadUnaligned<Vector<byte>>(src + srcOffset).CopyTo(dst + dstOffset);
                if (count == _vectorSpan) return;
                count -= _vectorSpan;
                srcOffset += _vectorSpan;
                dstOffset += _vectorSpan;
            }
            if (count > 0)
            {
                {
                    var pSrc = src + srcOffset;
                    var pDst = dst + dstOffset;

                    if (count >= _longSpan)
                    {
                        var lpSrc = (long*)pSrc;
                        var ldSrc = (long*)pDst;

                        if (count < _longSpan2)
                        {
                            count -= _longSpan;
                            pSrc += _longSpan;
                            pDst += _longSpan;
                            *ldSrc = *lpSrc;
                        }
                        else if (count < _longSpan3)
                        {
                            count -= _longSpan2;
                            pSrc += _longSpan2;
                            pDst += _longSpan2;
                            *ldSrc = *lpSrc;
                            *(ldSrc + 1) = *(lpSrc + 1);
                        }
                        else
                        {
                            count -= _longSpan3;
                            pSrc += _longSpan3;
                            pDst += _longSpan3;
                            *ldSrc = *lpSrc;
                            *(ldSrc + 1) = *(lpSrc + 1);
                            *(ldSrc + 2) = *(lpSrc + 2);
                        }
                    }
                    if (count >= _intSpan)
                    {
                        var ipSrc = (int*)pSrc;
                        var idSrc = (int*)pDst;
                        count -= _intSpan;
                        pSrc += _intSpan;
                        pDst += _intSpan;
                        *idSrc = *ipSrc;
                    }
                    while (count > 0)
                    {
                        count--;
                        *pDst = *pSrc;
                        pDst += 1;
                        pSrc += 1;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorizedCopy(ref byte dst, ref byte src, ulong length)
        {
            var count = (int)length;
            var srcOffset = (IntPtr)0;
            var dstOffset = (IntPtr)0;

            while (count >= _vectorSpan4)
            {
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset)));
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset + _vectorSpan)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset + _vectorSpan)));
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset + _vectorSpan2)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset + _vectorSpan2)));
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset + _vectorSpan3)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset + _vectorSpan3)));
                if (count == _vectorSpan4) return;
                count -= _vectorSpan4;
                srcOffset += _vectorSpan4;
                dstOffset += _vectorSpan4;
            }
            if (count >= _vectorSpan2)
            {
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset)));
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset + _vectorSpan)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset + _vectorSpan)));
                if (count == _vectorSpan2) return;
                count -= _vectorSpan2;
                srcOffset += _vectorSpan2;
                dstOffset += _vectorSpan2;
            }
            if (count >= _vectorSpan)
            {
                ReadUnaligned<Vector<byte>>(ref AddByteOffset(ref src, srcOffset)).CopyTo(AsPointer(ref AddByteOffset(ref dst, dstOffset)));
                if (count == _vectorSpan) return;
                count -= _vectorSpan;
                srcOffset += _vectorSpan;
                dstOffset += _vectorSpan;
            }
            if (count > 0)
            {
                {
                    ref var refSrc = ref AddByteOffset(ref src, srcOffset);
                    ref var refDst = ref AddByteOffset(ref dst, dstOffset);

                    if (count >= _longSpan)
                    {
                        ref var longRefSrc = ref AsRef<long>(refSrc);
                        ref var longRefDst = ref AsRef<long>(refDst);

                        if (count < _longSpan2)
                        {
                            count -= _longSpan;
                            refSrc += _longSpan;
                            refDst += _longSpan;
                            longRefDst = longRefSrc;
                        }
                        else if (count < _longSpan3)
                        {
                            count -= _longSpan2;
                            refSrc += _longSpan2;
                            refDst += _longSpan2;
                            longRefDst = longRefSrc;
                            Add(ref longRefDst, 1) = Add(ref longRefSrc, 1);
                        }
                        else
                        {
                            count -= _longSpan3;
                            refSrc += _longSpan3;
                            refDst += _longSpan3;
                            longRefDst = longRefSrc;
                            Add(ref longRefDst, 1) = Add(ref longRefSrc, 1);
                            Add(ref longRefDst, 2) = Add(ref longRefSrc, 2);
                        }
                    }
                    if (count >= _intSpan)
                    {
                        ref var intRefSrc = ref AsRef<int>(refSrc);
                        ref var intRefDst = ref AsRef<int>(refDst);
                        count -= _intSpan;
                        refSrc += _intSpan;
                        refDst += _intSpan;
                        intRefDst = intRefSrc;
                    }
                    while (count > 0)
                    {
                        count--;
                        refDst = refSrc;
                        refDst += 1;
                        refSrc += 1;
                    }
                }
            }
        }

        // without CopyChunk64 throughput is better in Aeron.NET IPC case

        //[StructLayout(LayoutKind.Sequential, Pack = 64, Size = 64)]
        //internal struct CopyChunk64 {
        //    private fixed byte _bytes[64];
        //}

        [StructLayout(LayoutKind.Sequential, Pack = 32, Size = 32)]
        internal struct CopyChunk32
        {
            private readonly long _l1;
            private readonly long _l2;
            private readonly long _l3;
            private readonly long _l4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryCopy(byte* destination, byte* source, uint length)
        {
            var pos = 0;
            int nextPos;
            // whithout this perf is better
            //nextPos = pos + 64;
            //while (nextPos <= length) {
            //    *(CopyChunk64*)(destination + pos) = *(CopyChunk64*)(source + pos);
            //    pos = nextPos;
            //    nextPos += 64;
            //}
            nextPos = pos + 32;
            while (nextPos <= length)
            {
                *(CopyChunk32*)(destination + pos) = *(CopyChunk32*)(source + pos);
                pos = nextPos;
                nextPos += 32;
            }
            nextPos = pos + 8;
            while (nextPos <= length)
            {
                *(long*)(destination + pos) = *(long*)(source + pos);
                pos = nextPos;
                nextPos += 8;
            }
            // whithout this perf is better
            //nextPos = pos + 4;
            //while (nextPos <= length) {
            //    *(int*)(destination + pos) = *(int*)(source + pos);
            //    pos = nextPos;
            //    nextPos += 4;
            //}
            while (pos < length)
            {
                *(destination + pos) = *(source + pos);
                pos++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryMove(byte* destination, byte* source, uint length)
        {
            if (destination < source)
            {
                var pos = 0;
                int nextPos;
                if (_wordSize == 8)
                {
                    nextPos = pos + 8;
                    while (nextPos <= length)
                    {
                        *(long*)(destination + pos) = *(long*)(source + pos);
                        pos = nextPos;
                        nextPos += 8;
                    }
                }
                else
                {
                    nextPos = pos + 4;
                    while (nextPos <= length)
                    {
                        *(int*)(destination + pos) = *(int*)(source + pos);
                        pos = nextPos;
                        nextPos += 4;
                    }
                }
                while (pos < length)
                {
                    *(destination + pos) = *(source + pos);
                    pos++;
                }
            }
            else if (destination > source)
            {
                var pos = (int)length;
                int nextPos;
                if (_wordSize == 8)
                {
                    nextPos = pos - 8;
                    while (nextPos >= 0)
                    {
                        *(long*)(destination + pos) = *(long*)(source + pos);
                        pos = nextPos;
                        nextPos -= 8;
                    }
                }
                else
                {
                    nextPos = pos - 4;
                    while (nextPos >= 0)
                    {
                        *(int*)(destination + pos) = *(int*)(source + pos);
                        pos = nextPos;
                        nextPos -= 4;
                    }
                }
                while (pos > 0)
                {
                    *(destination + pos) = *(source + pos);
                    pos--;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(IntPtr ptr, int len)
        {
            unchecked
            {
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