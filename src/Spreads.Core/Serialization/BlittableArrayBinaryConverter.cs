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

    internal static class BlittableArrayConverterFactory {
        public static IBinaryConverter<TElement[]> GenericCreate<TElement>() {
            return new BlittableArrayBinaryConverter<TElement>();
        }
        public static object Create(Type type) {
            var method = typeof(BlittableArrayConverterFactory).GetMethod("GenericCreate");
            var generic = method.MakeGenericMethod(type);
            return generic.Invoke(null, null);
        }
    }

    internal class BlittableArrayBinaryConverter<TElement> : IBinaryConverter<TElement[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
#pragma warning disable 618
        public byte Version => 0;
        private static readonly int ItemSize = TypeHelper<TElement>.Size;
#pragma warning restore 618

        public int SizeOf(TElement[] value, out MemoryStream temporaryStream) {
            if (ItemSize > 0) {
                temporaryStream = null;
                return 8 + ItemSize * value.Length;
            }
            throw new InvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
        }


        public unsafe int Write(TElement[] value, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (temporaryStream != null) throw new NotSupportedException("BlittableArrayBinaryConverter does not work with temp streams.");
            if (ItemSize > 0) {
                var totalSize = 8 + ItemSize * value.Length;
                if (!destination.HasCapacity(offset, totalSize)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                var ptr = destination.Data + (int)offset;

                var pinnedArray = GCHandle.Alloc(value, GCHandleType.Pinned);
                // size
                Marshal.WriteInt32(ptr, totalSize);
                // version
                Marshal.WriteByte(ptr + 4, Version);
                if (value.Length > 0) {
                    var source = Marshal.UnsafeAddrOfPinnedArrayElement(value, 0);
                    ByteUtil.MemoryCopy((byte*)(ptr + 8), (byte*)source, checked((uint)(ItemSize * value.Length)));
                }
                pinnedArray.Free();
                return totalSize;
            }
            throw new InvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
        }

        public unsafe int Read(IntPtr ptr, ref TElement[] value) {
            var totalSize = Marshal.ReadInt32(ptr);
            var version = Marshal.ReadByte(ptr + 4);
            if (version != 0) throw new NotSupportedException("ByteArrayBinaryConverter work only with version 0");
            if (ItemSize > 0) {
                var arraySize = (totalSize - 8) / ItemSize;
                if (arraySize > 0) {
                    var array = new TElement[arraySize];
                    var pinnedArray = GCHandle.Alloc(array, GCHandleType.Pinned);
                    var destination = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
                    var source = ptr + 8;
                    ByteUtil.MemoryCopy((byte*)destination, (byte*)source, checked((uint)(totalSize - 8)));
                    value = array;
                    pinnedArray.Free();
                } else {
                    value = new TElement[0];
                }
                return totalSize;
            }
            throw new InvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
        }
    }
}