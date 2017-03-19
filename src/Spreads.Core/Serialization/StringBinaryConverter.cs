// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;
using System.Buffers;

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

        public unsafe int Write(string value, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (compression == CompressionMethod.DefaultOrNone)
            {
                if (temporaryStream == null)
                {
                    fixed (char* charPtr = value)
                    {
                        var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                        if (!destination.HasCapacity(offset, totalLength)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                        var ptr = destination.Data + (int)offset;
                        // size
                        Marshal.WriteInt32(ptr, totalLength);
                        // version
                        Marshal.WriteByte(ptr + 4, Version);
                        // payload
                        var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)ptr + 8, totalLength);
                        Debug.Assert(totalLength == len + 8);
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

        public int Read(IntPtr ptr, ref string value)
        {
            var version = Marshal.ReadInt32(ptr + 4);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr);
            bool needReturn = false;
            byte[] buffer;
            if (length < RecyclableMemoryManager.StaticBufferSize)
            {
                buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            }
            else
            {
                needReturn = true;
                buffer = ArrayPool<byte>.Shared.Rent(length);
            }
            Marshal.Copy(ptr + 8, buffer, 0, length);
            value = Encoding.UTF8.GetString(buffer, 0, length - 8);
            if (needReturn) ArrayPool<byte>.Shared.Return(buffer, true);
            return length;
        }

        public byte Version => 0;
    }
}