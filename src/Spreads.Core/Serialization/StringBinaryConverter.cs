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

        public unsafe int SizeOf(in string value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            fixed (char* charPtr = value)
            {
                var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                temporaryStream = null;
                return totalLength;
            }
        }

        public unsafe int Write(in string value, IntPtr destination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (temporaryStream == null)
            {
                fixed (char* charPtr = value)
                {
                    var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);

                    // size
                    Marshal.WriteInt32(destination, totalLength);
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
                    WriteUnaligned((void*)(destination + 4), header);                        // payload
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)destination + 8, totalLength);
                    Debug.Assert(totalLength == len + 8);

                    return len + 8;
                }
            }

            temporaryStream.WriteToRef(ref AsRef<byte>((void*)destination));
            return checked((int)temporaryStream.Length);
        }

        public int Read(IntPtr ptr, out string value)
        {
            var version = Marshal.ReadInt32(ptr + 4);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr);
            OwnedPooledArray<byte> ownedPooledBuffer = Buffers.BufferPool.UseTempBuffer(length);
            var buffer = ownedPooledBuffer.Memory;
            var handle = buffer.Pin();

            try
            {
                if (ownedPooledBuffer.TryGetArray(out var segment))
                {
                    Marshal.Copy(ptr + 8, segment.Array, 0, length);

                    value = Encoding.UTF8.GetString(segment.Array, segment.Offset, length - 8);
                }
                else
                {
                    throw new ApplicationException();
                }
                return length;
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