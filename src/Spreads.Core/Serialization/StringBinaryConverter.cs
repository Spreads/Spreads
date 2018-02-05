// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Spreads.Serialization
{
    internal class StringBinaryConverter : IBinaryConverter<string>
    {
        public bool IsFixedSize => false;
        public int Size => 0;

        public unsafe int SizeOf(string value, out MemoryStream temporaryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (compression == CompressionMethod.DefaultOrNone)
            {
                fixed (char* charPtr = value)
                {
                    var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                    temporaryStream = null;
                    return totalLength;
                }
            }
            else
            {
                throw new NotImplementedException("TODO string compression");
            }
        }

        public unsafe int Write(string value, ref Memory<byte> destination, uint offset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (compression == CompressionMethod.DefaultOrNone)
            {
                if (temporaryStream == null)
                {
                    fixed (char* charPtr = value)
                    {
                        var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                        if (destination.Length < offset + totalLength) return (int)BinaryConverterErrorCode.NotEnoughCapacity;

                        var handle = destination.Retain(true);
                        var ptr = (IntPtr)handle.Pointer + (int)offset;

                        // size
                        Marshal.WriteInt32(ptr, totalLength);
                        // version
                        Marshal.WriteByte(ptr + 4, Version);
                        // payload
                        var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)ptr + 8, totalLength);
                        Debug.Assert(totalLength == len + 8);

                        handle.Dispose();

                        return len + 8;
                    }
                }
                else
                {
                    throw new NotSupportedException("StringBinaryConverter does not work with temp streams.");
                    //throw new NotImplementedException();
                    //temporaryStream.WriteToPtr(ptr);
                    //return checked((int)temporaryStream.Length);
                }
            }
            else
            {
                throw new NotImplementedException("TODO string compression");
            }
        }

        public int Read(IntPtr ptr, out string value)
        {
            var version = Marshal.ReadInt32(ptr + 4);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr);
            OwnedMemory<byte> ownedBuffer = Buffers.BufferPool.UseTempBuffer(length);
            var buffer = ownedBuffer.Memory;
            var handle = buffer.Retain(true);

            try
            {
                if (buffer.TryGetArray(out var segment))
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
                if (ownedBuffer != Buffers.BufferPool.StaticBuffer)
                {
                    Debug.Assert(ownedBuffer.IsDisposed);
                }
                else
                {
                    Debug.Assert(!ownedBuffer.IsDisposed);
                }
            }
        }

        public byte Version => 0;
    }
}