// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Reflection;
using Spreads.Buffers;
using System.Buffers;

namespace Spreads.Serialization
{

    // TODO BlittableArrayConverter should redirect here for uncompressed case as well

//    internal static class PreservedBufferArrayBinaryConverterFactory
//    {
//        public static PreservedBufferArrayBinaryConverterImpl<TElement> GenericCreate<TElement>()
//        {
//            return new PreservedBufferArrayBinaryConverterImpl<TElement>();
//        }

//        public static object Create(Type bufferType)
//        {
//            var elementType = bufferType.GetTypeInfo().GetGenericArguments()[0];
//            var method = typeof(PreservedBufferArrayBinaryConverterFactory).GetTypeInfo().GetMethod("GenericCreate");
//            var generic = method.MakeGenericMethod(elementType);
//            var converter = generic.Invoke(null, null);
//            return converter;
//        }
//    }

//    internal static class PreservedBufferArrayBinaryConverterFactory<TBuffer>
//    {
//        public static ICompressedArrayBinaryConverter<TBuffer> Instance =
//            (ICompressedArrayBinaryConverter<TBuffer>)(PreservedBufferArrayBinaryConverterFactory.Create(typeof(TBuffer)));
//        // TODO move the two methods (isPB, Dipose) from BufferPool. Use strongly-typed disposal to avoid boxing.
//    }


//    internal class PreservedBufferArrayBinaryConverterImpl<TElement> :
//        ICompressedArrayBinaryConverter<RetainedMemory<TElement>[]>, ICompressedArrayBinaryConverter<object>
//    {
//        public bool IsFixedSize => false;
//        public int Size => 0;
//#pragma warning disable 618
//        public byte Version => 0;
//        private static readonly int ItemSize = TypeHelper<TElement>.Size;

//        public int SizeOf(RetainedMemory<TElement>[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream, SerializationFormat compression = SerializationFormat.Binary)
//        {
//            throw new NotImplementedException("TODO PreservedBufferArray typed methods");
//        }

//        public int Write(RetainedMemory<TElement>[] value, int valueOffset, int valueCount, ref Memory<byte> destination, uint destinationOffset = 0, MemoryStream temporaryStream = null, SerializationFormat compression = SerializationFormat.Binary)
//        {
//            throw new NotImplementedException("TODO PreservedBufferArray typed methods");
//        }

//        public int Read(IntPtr ptr, out RetainedMemory<TElement>[] array, out int count, bool exactSize = false)
//        {
//            throw new NotImplementedException("TODO PreservedBufferArray typed methods");
//        }



//        public int SizeOf(object value, int valueOffset, int valueCount, out MemoryStream temporaryStream, SerializationFormat compression = SerializationFormat.Binary)
//        {
//            var typedValue = (RetainedMemory<TElement>[])value;
//            return SizeOf(typedValue, valueOffset, valueCount, out temporaryStream, compression);
//        }

//        public int Write(object value, int valueOffset, int valueCount, ref Memory<byte> destination, uint destinationOffset = 0, MemoryStream temporaryStream = null, SerializationFormat compression = SerializationFormat.Binary)
//        {
//            var typedValue = (RetainedMemory<TElement>[])value;
//            return Write(typedValue, valueOffset, valueCount, ref destination, destinationOffset, temporaryStream, compression);
//        }

//        public int Read(IntPtr ptr, out object array, out int count, bool exactSize = false)
//        {
//            var result = Read(ptr, out RetainedMemory<TElement>[] typedArray, out count, exactSize);
//            array = typedArray;
//            return result;
//        }


//#pragma warning restore 618


//    }
}