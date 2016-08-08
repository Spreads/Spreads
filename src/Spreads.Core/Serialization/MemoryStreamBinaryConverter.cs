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
    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(MemoryStream value, out MemoryStream temporaryStream) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");
            temporaryStream = null;
            return checked((int)value.Length + 8);
        }

        public unsafe int Write(MemoryStream value, ref DirectBuffer destination, uint offset, MemoryStream temporaryStream = null) {
            if (temporaryStream != null) throw new NotSupportedException("MemoryStreamBinaryConverter does not work with temp streams.");

            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");

            var totalLength = checked((int)value.Length + 8);
            if (!destination.HasCapacity(offset, totalLength)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            var ptr = destination.Data + (int)offset;
            // size
            Marshal.WriteInt32(ptr, totalLength);
            // version
            Marshal.WriteInt32(ptr + 4, Version);

            // payload
            ptr = ptr + 8;

            value.WriteToPtr(ptr);
            return totalLength;
        }

        public int Read(IntPtr ptr, ref MemoryStream value) {
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
