// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

#pragma warning disable 618

namespace Spreads.Serialization
{
    internal static class BinaryConverterExtensions
    {
        /// <summary>
        ///
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int WriteAsPtr<T>(this MemoryStream stream, T value)
        {
            var size = TypeHelper<T>.Size;
            if (size <= 0)
            {
                ThrowHelper.ThrowInvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            }
            // NB do not use a buffer pool here but instead use a thread-static buffer
            // that will grow to maximum size of a type. Fixed-size types are usually small.
            // Take/return is more expensive than the work we do with the pool here.

            var rm = Buffers.BufferPool.StaticBufferMemory;

            //var ownedBuffer = Buffers.BufferPool.StaticBuffer.pi;
            //var buffer = ownedBuffer.Memory;

            var size2 = TypeHelper<T>.Write(value, (IntPtr)rm.Pointer);

            Debug.Assert(size == size2);

            if (Buffers.BufferPool.StaticBuffer.TryGetArray(out var segment))
            {
                // TODO typecheck stream and use faster methods
                stream.Write(segment.Array, segment.Offset, size);
            }
            else
            {
                ThrowHelper.ThrowInvalidOperationException("Memory must be array based here by design");
            }

            // NB this is not needed as long as converter.Write guarantees overwriting all Size bytes.
            // //Array.Clear(_buffer, 0, size);
            return size;
        }

        /// <summary>
        /// Write entire stream to a pointer
        /// </summary>
        [Obsolete("Use AsRef + WriteToRef")]
        public static void WriteToPtr(this MemoryStream stream, IntPtr ptr)
        {
            stream.Position = 0;
            var ownedBuffer = Buffers.BufferPool.StaticBuffer;
            var position = 0;
            if (ownedBuffer.TryGetArray(out var segment))
            {
                // TODO Typecheck if RMS
                // TODO Use stream.TryGetBuffer
                // TODO Use VectorizedCopy
                int length;
                while ((length = stream.Read(segment.Array, segment.Offset, segment.Count)) > 0)
                {
                    // ByteUtil.VectorizedCopy(segment.Array,);
                    Marshal.Copy(segment.Array, segment.Offset, ptr + position, length);
                    position += length;
                }
            }
            else
            {
                ThrowHelper.ThrowInvalidOperationException("Memory must be array based here by design");
            }
        }

        public static void WriteToRef(this MemoryStream stream, ref byte destination)
        {
            if (stream is RecyclableMemoryStream rms)
            {
                var position = 0;
                foreach (var segment in rms.Chunks)
                {
                    ByteUtil.VectorizedCopy(ref AddByteOffset(ref destination, (IntPtr)position),
                        ref segment.Array[segment.Offset], (ulong)segment.Count);
                    position += segment.Count;
                }
            }
            else
            {
                stream.Position = 0;
                var buffer = BufferPool.StaticBuffer.Array.Length >= stream.Length
                    ? BufferPool.StaticBuffer.Array
                    : BufferPool<byte>.Rent(checked((int)stream.Length));
                var position = 0;

                int length;
                while ((length = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ByteUtil.VectorizedCopy(ref AddByteOffset(ref destination, (IntPtr)position),
                        ref buffer[0], (ulong)length);
                    position += length;
                }
            }
        }
    }
}