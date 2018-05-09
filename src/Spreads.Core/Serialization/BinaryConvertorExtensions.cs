// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable 618

namespace Spreads.Serialization
{
    internal static class BinaryConverterExtensions
    {
        /// <summary>
        ///
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteAsPtr<T>(this MemoryStream stream, T value)
        {
            var size = TypeHelper<T>.Size;
            if (size <= 0) throw new InvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            // NB do not use a buffer pool here but instead use a thread-static buffer
            // that will grow to maximum size of a type. Fixed-size types are usually small.
            // Take/return is more expensive than the work we do with the pool here.
            var ownedBuffer = Buffers.BufferPool.StaticBuffer;
            var buffer = ownedBuffer.Memory;
            
            var size2 = TypeHelper<T>.Write(value, ref buffer);
            Debug.Assert(size == size2);

            if (ownedBuffer.TryGetArray(out var segment))
            {
                stream.Write(segment.Array, 0, size);
            }
            else
            {
                throw new ApplicationException("Buffer must be array based here by design");
            }

            // NB this is not needed as long as converter.Write guarantees overwriting all Size bytes.
            // //Array.Clear(_buffer, 0, size);
            return size;
        }

        /// <summary>
        /// Write entire stream to a pointer
        /// </summary>
        public static void WriteToPtr(this MemoryStream stream, IntPtr ptr)
        {
            stream.Position = 0;
            var ownedBuffer = Buffers.BufferPool.StaticBuffer;
            int length;
            var position = 0;
            var buffer = ownedBuffer.Memory;
            if (ownedBuffer.TryGetArray(out var segment))
            {
                while ((length = stream.Read(segment.Array, segment.Offset, segment.Count)) > 0)
                {
                    Marshal.Copy(segment.Array, segment.Offset, ptr + position, length);
                    position += length;
                }
            }
            else
            {
                throw new ApplicationException("Buffer must be array based here by design");
            }
        }
    }
}