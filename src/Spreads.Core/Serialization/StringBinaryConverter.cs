// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal class StringBinaryConverter : IBinaryConverter<string>
    {
        public bool IsFixedSize => false;
        public int Size => 0;

        public unsafe int SizeOf(in string map, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            fixed (char* charPtr = map)
            {
                var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, map.Length);
                temporaryStream = null;
                return totalLength;
            }
        }

        public unsafe int Write(in string value, IntPtr pinnedDestination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (temporaryStream == null)
            {
                fixed (char* charPtr = value)
                {
                    var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);

                    // size
                    Marshal.WriteInt32(pinnedDestination, totalLength);
                    // version
                    var header = new DataTypeHeader
                    {
                        VersionAndFlags = {
                                Version = 0,
                                IsBinary = true,
                                IsDelta = false,
                                IsCompressed = false },
                        TypeEnum = TypeEnum.String
                    };
                    WriteUnaligned((void*)(pinnedDestination + 4), header);                        // payload
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)pinnedDestination + 8, totalLength);
                    Debug.Assert(totalLength == len + 8);

                    return len + 8;
                }
            }

            temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
            return checked((int)temporaryStream.Length);
        }

        public unsafe int Read(IntPtr ptr, out string value)
        {
            var totalSize = ReadUnaligned<int>((void*)ptr);
            var header = ReadUnaligned<DataTypeHeader>((void*)(ptr + 4));
            if (header.VersionAndFlags.Version != 0) throw new NotSupportedException();
            OwnedPooledArray<byte> ownedPooledBuffer = Buffers.BufferPool.UseTempBuffer(totalSize);
            var buffer = ownedPooledBuffer.Memory;
            var handle = buffer.Pin();

            try
            {
                if (ownedPooledBuffer.TryGetArray(out var segment))
                {
                    Marshal.Copy(ptr + 8, segment.Array, 0, totalSize);

                    value = Encoding.UTF8.GetString(segment.Array, segment.Offset, totalSize - 8);
                }
                else
                {
                    throw new ApplicationException();
                }
                return totalSize;
            }
            finally
            {
                handle.Dispose();
                if (ownedPooledBuffer != Buffers.BufferPool.StaticBuffer)
                {
                    Debug.Assert(ownedPooledBuffer.IsDisposed);
                }
                else
                {
                    Debug.Assert(!ownedPooledBuffer.IsDisposed);
                }
            }
        }

        public byte Version => 0;
    }
}