// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using Spreads.Buffers;

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

        public unsafe int Write(DateTime[] value, ref DirectBuffer destination, uint offset = 0, MemoryStream temporaryStream = null,
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
                for (var i = 0; i < value.Length; i++)
                {
                    *(DateTime*)(destination.Data + (int)offset + 8 + i * 8) = value[i];
                }
                destination.WriteInt32(offset, length);
                destination.WriteByte(offset + 4, Version);
                return length;
            }
            else
            {
                return CompressedArrayBinaryConverter<DateTime>.Instance.Write(value, 0, value.Length, ref destination, offset, temporaryStream, compression);
            }
        }

        public unsafe int Read(IntPtr ptr, ref DateTime[] value)
        {
            var isCompressed = ((*(int*)(ptr + 4)) & 0b0000_0001) != 0;
            if (!isCompressed)
            {
                var len = *(int*)ptr;
                var arrLen = (len - 8) / 8;
                if (value == null || value.Length < arrLen)
                {
                    value = new DateTime[arrLen];
                }
                for (int i = 0; i < arrLen; i++)
                {
                    value[i] = *(DateTime*)(ptr + 8 + i * 8);
                }
                return len;
            }
            else
            {
                ArraySegment<DateTime> tmp = default(ArraySegment<DateTime>);
                var len = CompressedArrayBinaryConverter<DateTime>.Instance.Read(ptr, ref tmp, true);
                value = tmp.Array;
                return len;
            }
        }
    }
}