// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Blosc;
using Spreads.Buffers;
using System;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    public static unsafe partial class BinarySerializer
    {
#if NETCOREAPP2_1
        internal static readonly bool IsBrotliSupported = true;
#else

        //
        internal static readonly bool IsBrotliSupported = false;

#endif

#if NETCOREAPP2_1

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteBrotli(DirectBuffer source, DirectBuffer destination)
        {
            if (BrotliEncoder.TryCompress(source.Span, destination.Span, out var bytesWritten,
                Settings.BrotliCompressionLevel, 10))
            {
                return bytesWritten;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadBrotli(DirectBuffer source, DirectBuffer destination)
        {
            if (BrotliDecoder.TryDecompress(source.Span, destination.Span, out var bytesWritten))
            {
                return bytesWritten;
            }

            return 0;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteLz4(DirectBuffer source, DirectBuffer destination)
        {
            return BloscMethods.compress_lz4(source._data, source._length, destination._data, destination._length, Settings.LZ4CompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadLz4(DirectBuffer source, DirectBuffer destination)
        {
            return BloscMethods.decompress_lz4(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZstd(DirectBuffer source, DirectBuffer destination)
        {
            return BloscMethods.compress_zstd(source._data, source._length, destination._data, destination._length, Settings.ZstdCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZstd(DirectBuffer source, DirectBuffer destination)
        {
            return BloscMethods.decompress_zstd(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZlib(DirectBuffer source, DirectBuffer destination)
        {
            return BloscMethods.compress_zlib(source._data, source._length, destination._data, destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZlib(DirectBuffer source, DirectBuffer destination)
        {
            return BloscMethods.decompress_zlib(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle(DirectBuffer source, DirectBuffer destination, byte typeSize)
        {
            BloscMethods.shuffle((IntPtr)typeSize, source._length, source._data, destination._data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Unshuffle(DirectBuffer source, DirectBuffer destination, byte typeSize)
        {
            BloscMethods.unshuffle((IntPtr)typeSize, source._length, source._data, destination._data);
        }
    }
}
