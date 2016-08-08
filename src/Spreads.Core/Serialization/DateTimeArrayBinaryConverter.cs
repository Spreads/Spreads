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
using System.Diagnostics;
using System.IO;
using Spreads.Buffers;

namespace Spreads.Serialization
{
    internal class DateTimeArrayBinaryConverter : IBinaryConverter<DateTime[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public byte Version => 0;
        public int SizeOf(DateTime[] value, out MemoryStream temporaryStream) {
            temporaryStream = null;
            return (8 + value.Length * 8);
        }

        public unsafe int Write(DateTime[] value, ref DirectBuffer destination, uint offset = 0, MemoryStream temporaryStream = null) {
            Debug.Assert(temporaryStream == null);
            var length = 8 + value.Length * 8;
            if (destination.Length < offset + length) {
                return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            }
            for (var i = 0; i < value.Length; i++) {
                *(DateTime*)(destination.Data + (int)offset + 8 + i * 8) = value[i];
            }
            destination.WriteInt32(offset, length);
            destination.WriteByte(offset + 4, Version);
            return length;
        }

        public unsafe int Read(IntPtr ptr, ref DateTime[] value) {
            var len = *(int*)ptr;
            var arrLen = (len - 8) / 8;
            value = new DateTime[arrLen];
            for (int i = 0; i < arrLen; i++) {
                value[i] = *(DateTime*)(ptr + 8 + i * 8);
            }
            return len;
        }
    }
}