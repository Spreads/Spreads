using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization {

    // new version of serializer
    // uses TypeHelper to get statically cached reflection metadata
    // for default format, uses recursive BSON + Direct TypeHelper methods
    // for other formats just applies JSON.NET/Protobuf
    // Reused array pools from Spreads or uses thread static buffers where appropriate

    // TODO there is a mess if we return SizeOf with or without 8 bytes header
    // should be simple - SiezOf - binary size including the 8 bytes,
    // length value in the header - only payload without the header

    public static class BinarySerializer
    {

        private static ConditionalWeakTable<object, MemoryStream> _cache =
            new ConditionalWeakTable<object, MemoryStream>();

        public static int SizeOf<T>(T value) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="memoryStream"></param>
        /// <returns></returns>
        public static int SizeOf<T>(T value, out MemoryStream memoryStream)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Write binary representation of value into destination.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="destination"></param>
        /// <param name="memoryStream"></param>
        /// <returns>Length of written bytes</returns>
        public static int Serialize<T>(T value, IntPtr destination, MemoryStream memoryStream) {
            throw new NotImplementedException();
        }


        // use TypeHelper
        // if we know the SizeOf and it is fixed, write directly to pointer
        // if there is an interface, call TH.SizeOf with a null MS, pass it to ToPtr then after bound checks
        // if TypeHelper could not do the work itself, call BSON/JSON (which by default will try to use TH)

        public static int Serialize<T>(T value, DirectBuffer destination, uint offset) {
            // TODO length check
            throw new NotImplementedException();
        }

        public static int Serialize<T>(T value, byte[] destination, uint offset) {
            // TODO length check
            throw new NotImplementedException();
        }

    }
}
