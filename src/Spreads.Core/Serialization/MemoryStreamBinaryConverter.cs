// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream>
    {
        public bool IsFixedSize => false;
        public int Size => 0;

        public int SizeOf(MemoryStream value, out MemoryStream temporaryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");
            temporaryStream = null;
            return checked((int)value.Length + 8);
        }

        public unsafe int Write(MemoryStream value, ref Memory<byte> destination, uint offset, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (temporaryStream != null) throw new NotSupportedException("MemoryStreamBinaryConverter does not work with temp streams.");

            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");

            var totalLength = checked((int)value.Length + 8);
            if (destination.Length < offset + totalLength)
            {
                return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            }

            var handle = destination.Retain(true);

            var ptr = (IntPtr)handle.PinnedPointer + (int)offset;
            // size
            Marshal.WriteInt32(ptr, totalLength);
            // version
            Marshal.WriteInt32(ptr + 4, Version);

            // payload
            ptr = ptr + 8;
            value.WriteToPtr(ptr);

            handle.Dispose();

            return totalLength;
        }

        public int Read(IntPtr ptr, out MemoryStream value)
        {
            var length = Marshal.ReadInt32(ptr);
            var version = Marshal.ReadInt32(ptr + 4);
            if (version != 0) throw new NotSupportedException();
            var bytes = new byte[length - 8];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            value = new MemoryStream(bytes);
            return length + 8;
        }

        public byte Version => 0;
    }
}