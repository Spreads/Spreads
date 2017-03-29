// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Utils;
using System.Buffers;

namespace Spreads.Serialization
{
    internal static class PreservedBufferArrayBinaryConverterFactory
    {
        public static ICompressedArrayBinaryConverter<PreservedBuffer<TElement>[]> GenericCreate<TElement>()
        {
            return new PreservedBufferArrayBinaryConverterImpl<TElement>();
        }

        public static object Create(Type bufferType)
        {
            var elementType = bufferType.GetTypeInfo().GetGenericArguments()[0];
            var method = typeof(PreservedBufferArrayBinaryConverterFactory).GetTypeInfo().GetMethod("GenericCreate");
            var generic = method.MakeGenericMethod(elementType);
            return generic.Invoke(null, null);
        }
    }


    internal static class PreservedBufferArrayBinaryConverterFactory<TBuffer>
    {

    }


    /// <summary>
    /// Type is generic, but its methods are non-generic and cast objects to generics, the casts must succeed by construction.
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    internal class PreservedBufferArrayBinaryConverterImpl<TElement> : ICompressedArrayBinaryConverter<PreservedBuffer<TElement>[]> //, IBinaryConverter<object>
    {
        public bool IsFixedSize => false;
        public int Size => 0;
#pragma warning disable 618
        public byte Version => 0;
        private static readonly int ItemSize = TypeHelper<TElement>.Size;

        public int SizeOf(PreservedBuffer<TElement>[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            throw new NotImplementedException();
        }

        public int Write(PreservedBuffer<TElement>[] value, int valueOffset, int valueCount, ref Buffer<byte> destination, uint destinationOffset = 0, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            throw new NotImplementedException();
        }
#pragma warning restore 618


    }
}