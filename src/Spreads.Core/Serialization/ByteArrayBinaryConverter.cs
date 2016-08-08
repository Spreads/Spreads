/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Runtime.InteropServices;
using Spreads.Buffers;

namespace Spreads.Serialization {
    internal class ByteArrayBinaryConverter : IBinaryConverter<byte[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(byte[] value, out MemoryStream temporaryStream) {
            temporaryStream = null;
            return value.Length + 8;
        }

        public int Write(byte[] value, ref DirectBuffer destination, uint offset = 0, MemoryStream temporaryStream = null) {
            if (temporaryStream != null) throw new NotSupportedException("ByteArrayBinaryConverter does not work with temp streams.");
            var totalSize = value.Length + 8;
            if (!destination.HasCapacity(offset, totalSize)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            var ptr = destination.Data + (int)offset;
            // size
            Marshal.WriteInt32(ptr, totalSize);
            // version
            Marshal.WriteByte(ptr + 4, Version);
            // payload
            Marshal.Copy(value, 0, ptr + 8, value.Length);
            return totalSize;
        }

        public int Read(IntPtr ptr, ref byte[] value) {
            var totalSize = Marshal.ReadInt32(ptr);
            var version = Marshal.ReadByte(ptr + 4);
            if (version != 0) throw new NotSupportedException("ByteArrayBinaryConverter work only with version 0");
            var bytes = new byte[totalSize - 8];
            Marshal.Copy(ptr + 8, bytes, 0, bytes.Length);
            value = bytes;
            return totalSize;
        }

        public byte Version => 0;
    }
}