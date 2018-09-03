using System;
using System.IO;
using Spreads.DataTypes;

namespace Spreads.Serialization
{
    internal interface IArrayBinaryConverter<TElement>
    {
        byte ConverterVersion { get; }

        int SizeOf(TElement[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default);

        int Write(TElement[] value, int valueOffset, int valueCount, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default);

        int Read(IntPtr ptr, out TElement[] array, out int count, out Timestamp timestamp, bool exactSize = true);
    }
}