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

        public unsafe int SizeOf(string value, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            var tsSize = (long)timestamp == default ? 0 : Timestamp.Size;
            fixed (char* charPtr = value)
            {
                var totalLength = 8 + tsSize + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                temporaryStream = null;
                return totalLength;
            }
        }

        public unsafe int Write(string value, IntPtr pinnedDestination, MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            if (temporaryStream == null)
            {
                var tsSize = (long)timestamp == default ? 0 : Timestamp.Size;

                fixed (char* charPtr = value)
                {
                    var utf8len = Encoding.UTF8.GetByteCount(charPtr, value.Length);
                    var totalLength = 8 + tsSize + utf8len;

                    // version
                    var header = new DataTypeHeader
                    {
                        VersionAndFlags = {
                                Version = 0,
                                IsBinary = true,
                                IsDelta = false,
                                IsCompressed = false,
                                IsTimestamped = tsSize > 0
                        },
                        TypeEnum = TypeEnum.String
                    };
                    WriteUnaligned((void*)pinnedDestination, header);

                    // payload length
                    WriteUnaligned((void*)(pinnedDestination + 4), tsSize + utf8len);

                    if (tsSize > 0)
                    {
                        // payload length
                        WriteUnaligned((void*)(pinnedDestination + 8), timestamp);
                    }

                    // payload
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)pinnedDestination + 8 + tsSize, utf8len);
                    Debug.Assert(totalLength == 8 + tsSize + len);

                    return totalLength;
                }
            }

            temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
            return checked((int)temporaryStream.Length);
        }

        public unsafe int Read(IntPtr ptr, out string value, out Timestamp timestamp)
        {
            var header = ReadUnaligned<DataTypeHeader>((void*)ptr);
            var payloadLength = ReadUnaligned<int>((void*)(ptr + 4));
            var position = 8;
            var tsSize = 0;
            if (header.VersionAndFlags.IsTimestamped)
            {
                tsSize = Timestamp.Size;
                timestamp = ReadUnaligned<Timestamp>((void*)(ptr + position));
                position += 8;
            }
            else
            {
                timestamp = default;
            }

            if (header.VersionAndFlags.Version != 0)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            var ownedPooledBuffer = BufferPool.UseTempBuffer(payloadLength);
            var buffer = ownedPooledBuffer.Memory;
            var handle = buffer.Pin();

            try
            {
                Marshal.Copy(ptr + position, ownedPooledBuffer.Array, 0, payloadLength - tsSize);

                value = Encoding.UTF8.GetString(ownedPooledBuffer.Array, 0, payloadLength - tsSize);

                return 8 + payloadLength;
            }
            finally
            {
                handle.Dispose();
                if (ownedPooledBuffer != BufferPool.StaticBuffer)
                {
                    Debug.Assert(ownedPooledBuffer.IsDisposed);
                }
                else
                {
                    Debug.Assert(!ownedPooledBuffer.IsDisposed);
                }
            }
        }

        public byte ConverterVersion => 0;
    }
}