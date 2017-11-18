// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    internal class DateTimeArrayBinaryConverter : IBinaryConverter<DateTime[]>
    {
        public bool IsFixedSize => false;
        public int Size => 0;
        public byte Version => 0;

        public int SizeOf(DateTime[] value, out MemoryStream temporaryStream,
            CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (compression == CompressionMethod.DefaultOrNone)
            {
                temporaryStream = null;
                return (8 + value.Length * 8);
            }
            else
            {
                return CompressedArrayBinaryConverter<DateTime>.Instance.SizeOf(value, 0, value.Length, out temporaryStream, compression);
            }
        }

        public unsafe int Write(DateTime[] value, ref Memory<byte> destination, uint offset = 0, MemoryStream temporaryStream = null,
            CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (compression == CompressionMethod.DefaultOrNone)
            {
                Debug.Assert(temporaryStream == null);
                var length = 8 + value.Length * 8;
                if (destination.Length < offset + length)
                {
                    return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                }

                var handle = destination.Retain(true);
                try
                {
                    var ptr = (IntPtr)handle.PinnedPointer + (int)offset;

                    for (var i = 0; i < value.Length; i++)
                    {
                        *(DateTime*)(ptr + 8 + i * 8) = value[i];
                    }
                    // size
                    Marshal.WriteInt32(ptr, length);
                    // version
                    Debug.Assert(Version == 0, "all flags and version are zero for default impl");
                    Marshal.WriteByte(ptr + 4, Version);

                    return length;
                }
                finally
                {
                    handle.Dispose();
                }
            }
            else
            {
                return CompressedArrayBinaryConverter<DateTime>.Instance.Write(value, 0, value.Length, ref destination, offset, temporaryStream, compression);
            }
        }

        public unsafe int Read(IntPtr ptr, out DateTime[] value)
        {
            var isCompressed = ((*(int*)(ptr + 4)) & 0b0000_0001) != 0;
            if (!isCompressed)
            {
                var len = *(int*)ptr;
                var arrLen = (len - 8) / 8;
                value = new DateTime[arrLen];

                for (int i = 0; i < arrLen; i++)
                {
                    value[i] = *(DateTime*)(ptr + 8 + i * 8);
                }
                return len;
            }
            else
            {
                var len = CompressedArrayBinaryConverter<DateTime>.Instance.Read(ptr, out var tmp, out int _, true);
                value = tmp;
                return len;
            }
        }
    }
}