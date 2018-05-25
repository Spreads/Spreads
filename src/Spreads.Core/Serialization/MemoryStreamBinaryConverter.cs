// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream>
    {
        public bool IsFixedSize => false;
        public int Size => -1;

        public int SizeOf(in MemoryStream map, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            if (map.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");
            temporaryStream = null;
            return checked((int)map.Length + 8);
        }

        public unsafe int Write(in MemoryStream value, IntPtr pinnedDestination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (temporaryStream != null) throw new NotSupportedException("MemoryStreamBinaryConverter does not work with temp streams.");

            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");

            var totalLength = checked((int)value.Length + 8);

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
                TypeEnum = TypeEnum.Binary
            };
            WriteUnaligned((void*)(pinnedDestination + 4), header);

            // payload
            value.WriteToRef(ref AsRef<byte>((void*)(pinnedDestination + 8)));

            return totalLength;
        }

        public int Read(IntPtr ptr, out MemoryStream value)
        {
            var length = Marshal.ReadInt32(ptr);
            var version = Marshal.ReadInt32(ptr + 4);
            if (version != 0) throw new NotSupportedException();
            // TODO Use RMS large buffer
            var bytes = new byte[length - 8];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            value = new MemoryStream(bytes);
            return length + 8;
        }

        public byte Version => 0;
    }
}