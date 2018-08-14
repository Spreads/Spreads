// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;
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

            if (stream is RecyclableMemoryStream rms && rms.IsSingleChunk && rms.CapacityInternal - rms.PositionInternal > size)
            {
                WriteUnaligned(ref rms.SingleChunk[rms.PositionInternal], value);
                if (rms.PositionInternal + size > rms.LengthInternal)
                {
                    rms.SetLengthInternal(rms.PositionInternal + size);
                }
                else
                {
                    rms.PositionInternal = rms.PositionInternal + size;
                }
            }
            else
            {
                // NB do not use a buffer pool here but instead use a thread-static buffer
                // that will grow to maximum size of a type. Fixed-size types are usually small ( < 255 bytes).
                // Take/return is more expensive than the work we do with the pool here.

                var rm = Buffers.BufferPool.StaticBufferMemory;

                //var ownedBuffer = Buffers.BufferPool.StaticBuffer.pi;
                //var buffer = ownedBuffer.Memory;

                WriteUnaligned(rm.Pointer, value);

                // TODO typecheck stream and use faster methods
                stream.Write(BufferPool.StaticBuffer.Array, 0, size);
            }
            // NB this is not needed as long as converter.Write guarantees overwriting all Size bytes.
            // //Array.Clear(_buffer, 0, size);
            return size;
        }

        /// <summary>
        /// Write entire stream to a pointer
        /// </summary>
        [Obsolete("Use AsRef + WriteToRef")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToPtr(this MemoryStream stream, IntPtr ptr)
        {
            stream.Position = 0;
            var ownedBuffer = BufferPool.StaticBuffer;
            var position = 0;

            // TODO Typecheck if RMS
            // TODO Use stream.TryGetBuffer
            // TODO Use VectorizedCopy
            int length;
            while ((length = stream.Read(BufferPool.StaticBuffer.Array, 0, BufferPool.StaticBuffer.Array.Length)) > 0)
            {
                // ByteUtil.VectorizedCopy(segment.Array,);
                Marshal.Copy(BufferPool.StaticBuffer.Array, 0, ptr + position, length);
                position += length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToRef(this MemoryStream stream, ref byte destination)
        {
            if (stream is RecyclableMemoryStream rms)
            {
                if (rms.IsSingleChunk)
                {
                    CopyBlockUnaligned(ref destination,
                        ref rms.SingleChunk[0], checked((uint)rms.LengthInternal));
                }
                else
                {
                    var position = 0;
                    foreach (var segment in rms.Chunks)
                    {
                        CopyBlockUnaligned(ref AddByteOffset(ref destination, (IntPtr)position),
                            ref segment.Array[segment.Offset], checked((uint)segment.Count));
                        position += segment.Count;
                    }
                }
            }
            else
            {
                WriteToRefSlow(stream, ref destination);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteToRefSlow(MemoryStream stream, ref byte destination)
        {
            stream.Position = 0;
            var buffer = BufferPool.StaticBuffer.Array.Length >= stream.Length
                ? BufferPool.StaticBuffer.Array
                : BufferPool<byte>.Rent(checked((int)stream.Length));
            var position = 0;

            int length;
            while ((length = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                CopyBlockUnaligned(ref AddByteOffset(ref destination, (IntPtr)position),
                    ref buffer[0], checked((uint)length));
                position += length;
            }

            if (BufferPool.StaticBuffer.Array.Length < stream.Length)
            {
                BufferPool<byte>.Return(buffer);
            }
        }
    }
}