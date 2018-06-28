using System;
using System.IO;

namespace Spreads.Serialization
{
    internal interface IArrayBinaryConverter<TElement>
    {
        byte Version { get; }

        int SizeOf(in TElement[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary);

        int Write(in TElement[] value, int valueOffset, int valueCount, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary);

        int Read(IntPtr ptr, out TElement[] array, out int count, bool exactSize = true);
    }
}