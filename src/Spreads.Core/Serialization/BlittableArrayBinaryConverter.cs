// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    internal static class BlittableArrayConverterFactory
    {
        public static IBinaryConverter<TElement[]> GenericCreate<TElement>()
        {
            return new BlittableArrayBinaryConverter<TElement>();
        }

        public static object Create(Type type)
        {
            var method = typeof(BlittableArrayConverterFactory).GetTypeInfo().GetMethod("GenericCreate");
            var generic = method.MakeGenericMethod(type);
            return generic.Invoke(null, null);
        }
    }

    internal class BlittableArrayBinaryConverter<TElement> : IBinaryConverter<TElement[]>
    {
        public bool IsFixedSize => false;
        public int Size => 0;
#pragma warning disable 618
        public byte Version => 0;
        private static readonly int ItemSize = TypeHelper<TElement>.Size;
#pragma warning restore 618

        public int SizeOf(TElement[] value, out MemoryStream temporaryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (ItemSize > 0)
            {
                if (compression == CompressionMethod.DefaultOrNone)
                {
                    temporaryStream = null;
                    return 8 + ItemSize * value.Length;
                }
                else
                {
                    return CompressedArrayBinaryConverter<TElement>.Instance.SizeOf(value, 0, value.Length, out temporaryStream, compression);
                }
            }
            throw new InvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
        }

        public unsafe int Write(TElement[] value, ref Memory<byte> destination, uint offset = 0u,
            MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ItemSize > 0)
            {
                if (compression == CompressionMethod.DefaultOrNone)
                {
                    if (temporaryStream != null) throw new NotSupportedException("Uncompressed BlittableArrayBinaryConverter does not work with temp streams.");

                    var totalSize = 8 + ItemSize * value.Length;
                    if (destination.Length < offset + totalSize)
                    {
                        return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                    }

                    var handle = destination.Retain(true);
                    try
                    {
                        var ptr = (IntPtr)handle.PinnedPointer + (int)offset;

                        var pinnedArray = GCHandle.Alloc(value, GCHandleType.Pinned);
                        // size
                        Marshal.WriteInt32(ptr, totalSize);
                        // version
                        Debug.Assert(Version == 0, "all flags and version are zero for default impl");
                        Marshal.WriteByte(ptr + 4, Version);
                        if (value.Length > 0)
                        {
                            var source = Marshal.UnsafeAddrOfPinnedArrayElement(value, 0);
                            ByteUtil.VectorizedCopy((byte*)(ptr + 8), (byte*)source, checked((uint)(ItemSize * value.Length)));
                        }
                        pinnedArray.Free();

                        return totalSize;
                    }
                    finally
                    {
                        handle.Dispose();
                    }
                }
                else
                {
                    return CompressedArrayBinaryConverter<TElement>.Instance.Write(value, 0, value.Length,
                        ref destination, offset, temporaryStream, compression);
                }
            }
            throw new InvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
        }

        public unsafe int Read(IntPtr ptr, out TElement[] value)
        {
            var totalSize = Marshal.ReadInt32(ptr);
            var versionFlags = Marshal.ReadByte(ptr + 4);
            var version = (byte)(versionFlags >> 4);
            var isCompressed = (versionFlags & 0b0000_0001) != 0;
            if (version != 0) throw new NotSupportedException("ByteArrayBinaryConverter work only with version 0");
            if (ItemSize > 0)
            {
                if (!isCompressed)
                {
                    var arraySize = (totalSize - 8) / ItemSize;
                    if (arraySize > 0)
                    {
                        var array = new TElement[arraySize];
                        var pinnedArray = GCHandle.Alloc(array, GCHandleType.Pinned);
                        var destination = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
                        var source = ptr + 8;
                        ByteUtil.VectorizedCopy((byte*)destination, (byte*)source, checked((uint)(totalSize - 8)));
                        value = array;
                        pinnedArray.Free();
                    }
                    else
                    {
                        value = new TElement[0];
                    }
                    return totalSize;
                }
                else
                {
                    var len = CompressedArrayBinaryConverter<TElement>.Instance.Read(ptr, out var tmp, out var count, true);
                    Debug.Assert(tmp.Length == count);
                    value = tmp;
                    return len;
                }
            }
            throw new InvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
        }
    }
}